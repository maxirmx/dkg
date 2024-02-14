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

//
// Class Schnorr implements the vanilla Schnorr signature scheme.
// See https://en.wikipedia.org/wiki/Schnorr_signature.
//
// The only difference regarding the vanilla reference is the computation of
// the response. This implementation adds the random component with the
// challenge times private key while classical approach substracts them.
//
// The resulting signature shall be  compatible with EdDSA verification algorithm
// when using the edwards25519 group.
//

using System.Security.Cryptography;

namespace dkg
{
    public static class Schnorr
    {
        public static byte[] Sign(IGroup g, IScalar privateKey, byte[] msg)
        {
            // create random secret k and public point commitment R
            var k = g.Scalar().Pick(g.RndStream());
            var R = g.Point().Base().Mul(k);

            // create hash(publicKey || R || msg)
            var publicKey = g.Point().Base().Mul(privateKey);
            var h = Hash(g, publicKey, R, msg);

            // compute response s = k + x*h
            var s = k.Add(privateKey.Mul(h));

            // return R || S

            var b = new MemoryStream();
            R.MarshalBinary(b);
            s.MarshalBinary(b);
            return b.ToArray();
        }

        public static string? VerifyWithChecks(IGroup g, IPoint publicKey, byte[] msg, byte[] sig)
        {
            const string invalidLength = "Schnorr: invalid length";
            const string invalidSignature = "Schnorr: invalid signature";

            var R = g.Point();
            var s = g.Scalar();
            using (var memstream = new MemoryStream(sig))
            {
                try
                {
                    R.UnmarshalBinary(memstream);
                    s.UnmarshalBinary(memstream);
                    if (memstream.Position != memstream.Length)
                    // Extra bytes in signature are not acceptable
                    {
                        return invalidLength;
                    }
                }
                catch
                // May be System.IO.EndOfStreamException
                // but also can fail during decoding if some constraints are not met
                {
                    return invalidLength;
                }
            }

            // recompute hash(publicKey || R || msg)
            var h = Hash(g, publicKey, R, msg);

            // compute S = g^s
            var S = g.Point().Base().Mul(s);
            // compute RAh = R + A^h
            var Ah = publicKey.Mul(h);
            var RAs = R.Add(Ah);

            if (!S.Equals(RAs))
            {
                return invalidSignature;
            }

            return null;
        }

        public static string? Verify(IGroup g, IPoint publicPoint, byte[] msg, byte[] sig)
        {
            return VerifyWithChecks(g, publicPoint, msg, sig);
        }

        public static IScalar Hash(IGroup g, IPoint publicPoint, IPoint r, byte[] msg)
        {
            using (var sha512 = SHA512.Create())
            {
                var b = new MemoryStream();
                r.MarshalBinary(b);
                publicPoint.MarshalBinary(b);
                BinaryWriter w = new BinaryWriter(b);
                w.Write(msg);
                var hash = sha512.ComputeHash(b.ToArray());
                return g.Scalar().SetBytes(hash);
            }
        }
    }
}
