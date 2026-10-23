using BooTrayal.Core.Rules;
using BooTrayal.Core.Voice;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class VoiceRulesTests
    {
        [Test]
        public void ChannelName_Format_AndNullCode()
        {
            Assert.AreEqual("bt-K7Q2XP-pos", VoiceRules.ChannelName("K7Q2XP", VoiceChannel.Positional));
            Assert.AreEqual("bt-K7Q2XP-meet", VoiceRules.ChannelName("K7Q2XP", VoiceChannel.Meeting));
            Assert.AreEqual("bt-K7Q2XP-ghost", VoiceRules.ChannelName("K7Q2XP", VoiceChannel.Ghost));
            Assert.IsNull(VoiceRules.ChannelName(null, VoiceChannel.Positional));
            Assert.IsNull(VoiceRules.ChannelName("", VoiceChannel.Meeting));
        }

        [Test]
        public void Joined_PerState()
        {
            CollectionAssert.AreEquivalent(new[] { VoiceChannel.Positional, VoiceChannel.Meeting }, VoiceRules.Joined(LifeState.Alive));
            CollectionAssert.AreEquivalent(new[] { VoiceChannel.Positional, VoiceChannel.Meeting, VoiceChannel.Ghost }, VoiceRules.Joined(LifeState.MiniGhost));
            CollectionAssert.AreEquivalent(new[] { VoiceChannel.Positional, VoiceChannel.Meeting }, VoiceRules.Joined(LifeState.Escaped));
            CollectionAssert.IsEmpty(VoiceRules.Joined(LifeState.Disconnected));
        }

        [Test]
        public void TransmitTo_EveryStateAndMeeting()
        {
            Assert.AreEqual(VoiceChannel.Positional, VoiceRules.TransmitTo(LifeState.Alive, false));
            Assert.AreEqual(VoiceChannel.Meeting, VoiceRules.TransmitTo(LifeState.Alive, true));
            Assert.AreEqual(VoiceChannel.Ghost, VoiceRules.TransmitTo(LifeState.MiniGhost, false));
            Assert.AreEqual(VoiceChannel.Ghost, VoiceRules.TransmitTo(LifeState.MiniGhost, true), "the living never hear mini ghosts");
            Assert.AreEqual(VoiceChannel.Positional, VoiceRules.TransmitTo(LifeState.Escaped, false));
            Assert.AreEqual(VoiceChannel.Meeting, VoiceRules.TransmitTo(LifeState.Escaped, true));
            Assert.IsNull(VoiceRules.TransmitTo(LifeState.Disconnected, false));
        }

        [Test]
        public void Loudness_Threshold_Clamp_NaN()
        {
            Assert.AreEqual(0f, VoiceRules.LoudnessFromEnergy(0.15));
            Assert.AreEqual(0f, VoiceRules.LoudnessFromEnergy(0.1));
            Assert.AreEqual(0.5f, VoiceRules.LoudnessFromEnergy(0.375), 1e-5f);
            Assert.AreEqual(1f, VoiceRules.LoudnessFromEnergy(0.6), 1e-5f);
            Assert.AreEqual(1f, VoiceRules.LoudnessFromEnergy(5.0));
            Assert.AreEqual(0f, VoiceRules.LoudnessFromEnergy(double.NaN));
            Assert.AreEqual(0f, VoiceRules.LoudnessFromEnergy(-1.0));
        }

        [Test]
        public void AcceptVoice_Interval()
        {
            Assert.IsTrue(VoiceRules.AcceptVoice(10.0, double.NegativeInfinity));
            Assert.IsFalse(VoiceRules.AcceptVoice(10.39, 10.0));
            Assert.IsTrue(VoiceRules.AcceptVoice(10.4, 10.0));
            Assert.IsFalse(VoiceRules.AcceptVoice(double.NaN, 10.0));
        }

        [Test]
        public void ChannelVolume_DefaultIsUnityGain()
        {
            Assert.AreEqual(-50, VoiceRules.ChannelVolume(0f), "slider at 0 is the quietest Vivox level");
            Assert.AreEqual(0, VoiceRules.ChannelVolume(0.8f), "the default setting plays voices as recorded");
            Assert.AreEqual(20, VoiceRules.ChannelVolume(1f), "the top of the slider is a mild boost");
            Assert.AreEqual(-25, VoiceRules.ChannelVolume(0.4f));
            Assert.AreEqual(-50, VoiceRules.ChannelVolume(-1f));
            Assert.AreEqual(20, VoiceRules.ChannelVolume(2f));
            Assert.AreEqual(0, VoiceRules.ChannelVolume(float.NaN), "a broken setting falls back to unity gain");
        }
    }
}
