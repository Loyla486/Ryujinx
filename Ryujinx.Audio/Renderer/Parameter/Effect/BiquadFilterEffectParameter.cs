//
// Copyright (c) 2019-2021 Ryujinx
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.
//

using Ryujinx.Audio.Renderer.Server.Effect;
using Ryujinx.Common.Memory;
using System.Runtime.InteropServices;

namespace Ryujinx.Audio.Renderer.Parameter.Effect
{
    /// <summary>
    /// <see cref="EffectInParameter.SpecificData"/> for <see cref="Common.EffectType.BiquadFilter"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BiquadFilterEffectParameter
    {
        /// <summary>
        /// The input channel indices that will be used by the <see cref="Dsp.AudioProcessor"/>.
        /// </summary>
        public Array6<byte> Input;

        /// <summary>
        /// The output channel indices that will be used by the <see cref="Dsp.AudioProcessor"/>.
        /// </summary>
        public Array6<byte> Output;

        /// <summary>
        /// Biquad filter numerator (b0, b1, b2).
        /// </summary>
        public Array3<short> Numerator;

        /// <summary>
        /// Biquad filter denominator (a1, a2).
        /// </summary>
        /// <remarks>a0 = 1</remarks>
        public Array2<short> Denominator;

        /// <summary>
        /// The total channel count used.
        /// </summary>
        public byte ChannelCount;

        /// <summary>
        /// The current usage status of the effect on the client side.
        /// </summary>
        public UsageState Status;
    }
}
