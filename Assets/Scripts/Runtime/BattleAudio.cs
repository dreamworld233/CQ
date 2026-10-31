using System.Collections.Generic;
using System.Linq;
using CQ.Core.Battle;
using UnityEngine;

namespace CQ.Runtime
{
    public enum BattleSound { Select, Skill, Ultimate, Hit, Heal, Victory, Defeat }

    public sealed class BattleAudio : MonoBehaviour
    {
        private AudioSource _source;
        private readonly Dictionary<BattleSound, AudioClip> _clips = new Dictionary<BattleSound, AudioClip>();
        public bool Muted => _source.mute;

        public static BattleAudio Build(Transform owner)
        {
            var go = new GameObject("BattleAudio", typeof(AudioSource), typeof(BattleAudio));
            go.transform.SetParent(owner, false);
            var audio = go.GetComponent<BattleAudio>();
            audio._source = go.GetComponent<AudioSource>();
            audio._source.playOnAwake = false;
            audio._source.spatialBlend = 0f;
            audio._source.volume = 0.25f;
            audio._clips.Add(BattleSound.Select, Tone("Select", 0.055f, 620f));
            audio._clips.Add(BattleSound.Skill, Tone("Skill", 0.10f, 330f, 490f));
            audio._clips.Add(BattleSound.Ultimate, Tone("Ultimate", 0.20f, 330f, 440f, 660f));
            audio._clips.Add(BattleSound.Hit, Tone("Hit", 0.075f, 95f));
            audio._clips.Add(BattleSound.Heal, Tone("Heal", 0.20f, 520f, 780f));
            audio._clips.Add(BattleSound.Victory, Tone("Victory", 0.36f, 392f, 494f, 587f, 784f));
            audio._clips.Add(BattleSound.Defeat, Tone("Defeat", 0.30f, 294f, 247f, 196f));
            return audio;
        }

        public void SetMuted(bool muted)
        {
            _source.mute = muted;
            if (muted) _source.Stop();
        }

        public void Play(BattleSound sound)
        {
            if (!Muted) _source.PlayOneShot(_clips[sound]);
        }

        public static BattleSound? SoundFor(ActionResult result)
        {
            if (result.Kind == BattleEventKind.Skipped) return null;
            if (result.ActionType == "ult") return BattleSound.Ultimate;
            if (result.UnitChanges.Any(change => change.HpDelta > 0)) return BattleSound.Heal;
            if (result.Hits.Count > 0) return BattleSound.Hit;
            return result.Kind == BattleEventKind.Skill ? BattleSound.Skill : (BattleSound?)null;
        }

        public void StopPlayback() => _source.Stop();

        private static AudioClip Tone(string name, float duration, params float[] notes)
        {
            const int sampleRate = 22050;
            var samples = new float[Mathf.CeilToInt(duration * sampleRate)];
            float phase = 0f;
            float noteDuration = duration / notes.Length;
            for (int i = 0; i < samples.Length; i++)
            {
                float time = (float)i / sampleRate;
                int note = Mathf.Min((int)(time / noteDuration), notes.Length - 1);
                float localTime = time - note * noteDuration;
                float envelope = Mathf.Clamp01(Mathf.Min(localTime / 0.005f, (noteDuration - localTime) / 0.025f));
                phase += 2f * Mathf.PI * notes[note] / sampleRate;
                samples[i] = Mathf.Sin(phase) * envelope * 0.35f;
            }
            samples[0] = samples[samples.Length - 1] = 0f;
            var clip = AudioClip.Create("CQ_Synth_" + name, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            foreach (var clip in _clips.Values)
                if (Application.isPlaying) Destroy(clip);
                else DestroyImmediate(clip);
        }
    }
}
