using Ryujinx.Audio.Renderer.Server.Voice;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ryujinx.Tests.Audio.Renderer.Server
{
    public class VoiceStateTests
    {
        [Fact]
        public void EnsureTypeSize()
        {
            Assert.True(0x220 >= Unsafe.SizeOf<VoiceState>());
        }
    }
}
