﻿// Copyright (C) 2024 Maxim [maxirmx] Samsonov (www.sw.consulting)
// All rights reserved.
// This file is a part of dkg applcation
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
// 1. Redistributions of source code must retain the above copyright
// notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
// TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDERS OR CONTRIBUTORS
// BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using dkg.group;
using dkg.util;

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("dkgLibraryTests")]

namespace dkg.vss
{
    // Verifier receives a Deal from a Dealer, can reply with a Complaint, and can
    // collaborate with other Verifiers to reconstruct a secret.
    public class Verifier
    {
        internal IGroup G { get; }
        internal IScalar LongTermKey { get; }
        internal IPoint DealerKey { get; }
        internal IPoint PublicKey { get; }
        public IPoint[] Verifiers { get; set; }
        internal int Index { get; }
        internal byte[] HkdfContext { get; }
        internal Aggregator Aggregator { get; }
        public string? LastProcessingError { get; set; }

        // Constructor returns a Verifier out of:
        //   - its longterm secret key
        //   - the longterm dealer public key
        //   - the list of public key of verifiers. The list MUST include the public key of this Verifier also.
        //
        // The security parameter t of the secret sharing scheme is automatically set to
        // a default safe value. If a different t value is required, it is possible to set
        // it with `verifier.SetT()`.
        public Verifier(IGroup group, IScalar longterm, IPoint dealerKey, IPoint[] verifiers)
        {
            G = group;
            LastProcessingError = null;
            LongTermKey = longterm;
            DealerKey = dealerKey;
            PublicKey = G.Base().Mul(LongTermKey);
            Verifiers = verifiers;
            bool ok = false;
            int index = -1;

            for (int i = 0; i < verifiers.Length; i++)
            {
                if (verifiers[i].Equals(PublicKey))
                {
                    ok = true;
                    index = i;
                    break;
                }
            }
            if (!ok)
            {
                throw new ArgumentException("Verifier: public key not found in the list of verifiers");
            }

            Index = index;
            HkdfContext = DhHelper.Context(dealerKey, verifiers);
            Aggregator = new Aggregator(G, verifiers);
        }

        public Deal? DecryptDeal(EncryptedDeal encrypted)
        {
            LastProcessingError = null;
            // verify signature
            try
            {
                Schnorr.Verify(G, DealerKey, encrypted.DHKey, encrypted.Signature);

                // compute shared key and AES526-GCM cipher
                var dhKey = G.Point();
                dhKey.UnmarshalBinary(new MemoryStream(encrypted.DHKey));
                var pre = DhHelper.DhExchange(LongTermKey, dhKey);
                var nonce = encrypted.Nonce;
                var gcm = DhHelper.CreateAEAD(false, pre, HkdfContext, nonce);

                DhHelper.Decrypt(gcm, encrypted.Cipher, encrypted.Tag, out byte[] decrypted);

                var deal = new Deal();
                deal.UnmarshalBinary(new MemoryStream(decrypted));
                return deal;
            } 
            catch (Exception ex) 
            {
                LastProcessingError = $"DecryptDeal failed: {ex.Message}";
                return null;
            }
        }

        // ProcessEncryptedDeal decrypt the deal received from the Dealer.
        // If the deal is valid, i.e. the verifier can verify its shares
        // against the public coefficients and the signature is valid, an approval
        // response is returned and must be broadcasted to every participants
        // including the dealer.
        // If the deal itself is invalid, it returns a complaint response that must be
        // broadcasted to every other participants including the dealer.
        // If the deal has already been received, or the signature generation of the
        // response failed, it returns an error without any responses.
        public Response? ProcessEncryptedDeal(EncryptedDeal e)
        {
            try
            {
                var d = DecryptDeal(e);
                if (d == null) 
                    return null;

                if (d.SecShare.I != Index)
                {
                    LastProcessingError = "ProcessEncryptedDeal: got wrong index from deal";
                    return null;
                }

                var sid = VssTools.CreateSessionId(DealerKey, Verifiers, d.Commitments, d.T);
                Response r = new(sid, Index)
                {
                    Complaint = Aggregator.VerifyDeal(d, true)
                };
                if (r.Complaint == ComplaintCode.NoComplaint)
                {
                    r.Status = ResponseStatus.Approval;
                }
                else
                {
                    r.Status = ResponseStatus.Complaint;
                    LastProcessingError = Response.GetComplaintMessage(r.Complaint);
                    if (r.Complaint == ComplaintCode.AlreadyProcessed)
                        return null;
                }



                r.Signature = Schnorr.Sign(G,  LongTermKey, r.GetBytesForSignature());
                LastProcessingError = Aggregator.AddResponse(r);

                if (LastProcessingError != null) 
                    return null;
                return r;
            }
            catch (Exception ex)
            {
                LastProcessingError = $"DecryptDeal failed. {ex.Message}";
                return null;
            }
        }

        // Assuming other members of Verifier are defined here...

        // ErrNoDealBeforeResponse is an error returned if a verifier receives a
        // deal before having received any responses. For the moment, the caller must
        // be sure to have dispatched a deal before.
        public static readonly string ErrNoDealBeforeResponse = "verifier: need to receive deal before response";

        // ProcessResponse analyzes the given response. If it's a valid complaint, the
        // verifier should expect to see a Justification from the Dealer. It returns an
        // error if it's not a valid response.
        // Call `v.DealCertified()` to check if the whole protocol is finished.
        public string? ProcessResponse(Response resp)
        {
            if (Aggregator.Deal == null)
            {
                return ErrNoDealBeforeResponse;
            }
            return Aggregator.VerifyResponse(resp);
        }

        // SetTimeout marks the end of the protocol. The caller is expected to call this
        // after a long timeout so each verifier can still deem its share valid if
        // enough deals were approved. One should call `DealCertified()` after this
        // method in order to know if the deal is valid or the protocol should abort.
        public void SetTimeout()
        {
            Aggregator.Timeout = true;
        }

        // GetDeal returns the Deal that this verifier has received. It returns
        // null if the deal is not certified or there is not enough approvals.
        public Deal? GetDeal()
        {
            if (!Aggregator.DealCertified())
            {
                return null;
            }
            return Aggregator.Deal;
        }

        // ProcessJustification takes a DealerResponse and returns an error if
        // something went wrong during the verification. If it is the case, that
        // probably means the Dealer is acting maliciously. In order to be sure, call
        // `v.DealCertified()`.
        // Convert the given code to a member function in the Verifier class

        public string? ProcessJustification(Justification justification)
        {
            return Aggregator.VerifyJustification(justification);
        }
        // SetThreshold is used to specify the expected threshold *before* the verifier
        // receives anything. Sometimes, a verifier knows the treshold in advance and
        // should make sure the one it receives from the dealer is consistent. If this
        // method is not called, the first threshold received is considered as the
        // "truth".
        public void SetThreshold(int t)
        {
            Aggregator.T = t;
        }
        // SetResponseDkg is a method to allow DKG to use VSS
        // that works on basis of approval only.
        public void SetResponseDkg(int idx, ResponseStatus status)
        {
            Response r = new(Aggregator.SessionId, idx) 
            { 
                Status = status 
            };
            Aggregator.AddResponse(r);
        }

        public Dictionary<int, Response> Responses() 
        { 
            return Aggregator.Responses; 
        }

        public bool DealCertified()
        {
            return Aggregator.DealCertified();
        }

        public Deal? Deal()
        {
            return Aggregator.Deal;
        }
        public int[] MissingResponses()
        {
            return Aggregator.MissingResponses();
        }
    }

}
