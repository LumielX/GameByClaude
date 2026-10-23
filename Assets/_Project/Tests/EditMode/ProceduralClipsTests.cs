using System;
using System.Collections.Generic;
using BooTrayal.Game.Audio;
using NUnit.Framework;
using UnityEngine;

namespace BooTrayal.Tests
{
    public class ProceduralClipsTests
    {
        static IEnumerable<TestCaseData> Clips()
        {
            yield return new TestCaseData((Func<AudioClip>)ProceduralClips.Heartbeat, 1f).SetName("Heartbeat");
            yield return new TestCaseData((Func<AudioClip>)ProceduralClips.Wind, 4f).SetName("Wind");
            yield return new TestCaseData((Func<AudioClip>)ProceduralClips.Drone, 4f).SetName("Drone");
            yield return new TestCaseData((Func<AudioClip>)ProceduralClips.Screech, 0.8f).SetName("Screech");
            yield return new TestCaseData((Func<AudioClip>)ProceduralClips.Boo, 0.7f).SetName("Boo");
            yield return new TestCaseData((Func<AudioClip>)ProceduralClips.Ambience, 8f).SetName("Ambience");
            yield return new TestCaseData((Func<AudioClip>)ProceduralClips.Whispers, 6f).SetName("Whispers");
            yield return new TestCaseData((Func<AudioClip>)ProceduralClips.Bell, 2.5f).SetName("Bell");
            yield return new TestCaseData((Func<AudioClip>)ProceduralClips.UiClick, 0.08f).SetName("UiClick");
            yield return new TestCaseData((Func<AudioClip>)ProceduralClips.UiHover, 0.05f).SetName("UiHover");
        }

        [TestCaseSource(nameof(Clips))]
        public void Clip_HasExpectedLength_AndNoNaNOrClipping(Func<AudioClip> make, float seconds)
        {
            var clip = make();
            Assert.AreEqual(seconds, clip.length, 0.02f);
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            float peak = 0f;
            foreach (var v in data)
            {
                Assert.IsFalse(float.IsNaN(v));
                peak = Mathf.Max(peak, Mathf.Abs(v));
            }
            Assert.LessOrEqual(peak, 1f, "no clipping");
            Assert.Greater(peak, 0.05f, "not silent");
        }
    }
}
