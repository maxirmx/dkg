﻿using dkg.group;
using dkg.poly;
using dkg.vss;

namespace dkg
{
    // DistKeyShare holds the share of a distributed key for a participant.
    public class DistKeyShare(IPoint[] commits, PriShare share, IScalar[] privatePoly) : IEquatable<DistKeyShare>
    {
        // Coefficients of the public polynomial holding the public key.
        public IPoint[] Commits { get; set; } = commits;

        // Share of the distributed secret which is private information.
        public PriShare Share { get; set; } = share;

        // Coefficients of the private polynomial generated by the node holding the
        // share. The final distributed polynomial is the sum of all these
        // individual polynomials, but it is never computed.
        public IScalar[] PrivatePoly { get; set; } = privatePoly;

        // Public returns the public key associated with the distributed private key.
        public IPoint Public()
        {
            return Commits[0];
        }

        // PriShare implements the DistKeyShare interface (???) so either pedersen or
        // rabin dkg can be used with dss.
        public PriShare PriShare()
        {
            return Share;
        }

        // Commitments implements the dss.DistKeyShare interface (???) so either pedersen or
        // rabin dkg can be used with dss.
        public IPoint[] Commitments()
        {
            return Commits;
        }

        // Renew adds the new distributed key share g (with secret 0) to the distributed key share d.
        public DistKeyShare Renew(DistKeyShare g)
        {
            // Check G(0) = 0*G.
            if (!g.Public().Equals(Suite.G.Point().Base().Mul(Suite.G.Scalar().Zero())))
            {
                throw new DkgError("Wrong renewal function", GetType().Name);
            }

            // Check whether they have the same index
            if (Share.I != g.Share.I)
            {
                throw new DkgError("Not the same party", GetType().Name);
            }

            var newShare = Share.V.Add(g.Share.V);
            var newCommits = new IPoint[Commits.Length];
            for (int i = 0; i < newCommits.Length; i++)
            {
                newCommits[i] = Commits[i].Add(g.Commits[i]);
            }
            return new DistKeyShare(newCommits, new PriShare(Share.I, newShare), PrivatePoly);
        }

        public bool Equals(DistKeyShare? other)
        {
            if (other == null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (Commits.Length != other.Commits.Length)
                return false;

            for (int i = 0; i < Commits.Length; i++)
            {
                if (!Commits[i].Equals(other.Commits[i]))
                    return false;
            }
            return true;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as DistKeyShare);
        }

        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                foreach (var coeff in PrivatePoly)
                {
                    hash = hash * 23 + (coeff != null ? coeff.GetHashCode() : 0);
                }
                foreach (var commit in Commits)
                {
                    hash = hash * 23 + (commit != null ? commit.GetHashCode() : 0);
                }
                hash = hash * 23 + (Share != null ? Share.GetHashCode() : 0);
                return hash;
            }
        }
    }

    // Deal holds the Deal for one participant as well as the index of the issuing
    // Dealer.
    public class DistDeal(int index, EncryptedDeal encryptedDeal)
    {
        // Index of the Dealer in the list of participants
        public int Index { get; set; } = index;

        // Deal issued for another participant
        public EncryptedDeal VssDeal { get; set; } = encryptedDeal;

        // Signature over the whole message
        public byte[] Signature { get; set; } = [];

        // GetBytes returns a binary representation of this deal, which is the
        // message signed in a dkg deal.
        public byte[] GetBytes()
        {
            MemoryStream stream = new();
            MarshalBinary(stream);
            return stream.ToArray();
        }

        public void MarshalBinary(Stream s)
        {
            BinaryWriter bw = new(s);
            bw.Write(Index);
            VssDeal.MarshalBinary(s);
        }
    }

    // Response holds the Response from another participant as well as the index of
    // the target Dealer.
    public class DistResponse(int index, Response vssResponse)
    {
        // Index of the Dealer for which this response is for
        public int Index { get; set; } = index;

        // Response issued from another participant
        public Response VssResponse { get; set; } = vssResponse;
    }

    // Justification holds the Justification from a Dealer as well as the index of
    // the Dealer in question.
    public class DistJustification(int index, Justification vssJustification)
    {
        // Index of the Dealer who answered with this Justification
        public int Index { get; set; } = index;

        // Justification issued from the Dealer
        public Justification VssJustification { get; set; } = vssJustification;
    }
}
