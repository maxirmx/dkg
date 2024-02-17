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

namespace dkg
{
    public class Config
    {
        public IGroup G { get; set; }

        // Longterm is the LongTermKey secret key.
        public IScalar LongTermKey { get; set; }

        // Current group of share holders. It will be null for new DKG.
        public IPoint[] OldNodes { get; set; }

        // PublicCoeffs are the coefficients of the distributed polynomial needed
        // during the resharing protocol.
        public IPoint[] PublicCoeffs { get; set; }

        // Expected new group of share holders.
        public IPoint[] NewNodes { get; set; }

        // Share to refresh.
        //public DistKeyShare Share { get; set; }

        // The threshold to use in order to reconstruct the secret with the produced
        // shares.
        public int Threshold { get; set; }

        // OldThreshold holds the threshold value that was used in the previous
        // configuration.
        public int OldThreshold { get; set; }

        // Reader is an optional field that can hold a user-specified entropy source.
        public System.IO.Stream Reader { get; set; }

        // When UserReaderOnly it set to true, only the user-specified entropy source
        // Reader will be used.
        public bool UserReaderOnly { get; set; }
    }
}