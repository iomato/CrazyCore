﻿using Engine;
using Engine.Behaviors;
using Engine.Graphics;
using System.Numerics;
using Engine.Audio;
using System;

namespace CrazyCore
{
    public class ParticleVacuumEffect : Behavior
    {
        private ParticleSystem _particleSystem;
        private readonly Transform _vacuumTarget;
        private AudioSystem _audioSystem;
        private WaveFile _waveFile;
        private DateTime _lastSoundTime;
        private double _soundInterval = 0.02;
        private float _previousPitch = 0.5f;
        private float _pitchDirection = 1f;

        public float Acceleration { get; set; } = 150f;
        public float InitialExpulsionVelocity { get; set; } = 40f;
        public float DeletionDistance { get; set; } = .25f;
        public bool DestroyAfterParticlesEmpty { get; set; } = true;

        public ParticleVacuumEffect(ParticleSystem particleSystem, Transform vacuumTarget, WaveFile audioBuffer, float deletionDistance)
        {
            _particleSystem = particleSystem;
            _vacuumTarget = vacuumTarget;
            _waveFile = audioBuffer;
            DeletionDistance = deletionDistance;
            _particleSystem.ModifyAllParticles((ref ParticleState ps) =>
            {
                Vector3 direction = Vector3.Normalize(ps.Offset - _vacuumTarget.Position);
                ps.Velocity += (ps.Offset - _particleSystem.Transform.Position) * InitialExpulsionVelocity;
            });
        }

        protected override void Start(SystemRegistry registry)
        {
            _audioSystem = registry.GetSystem<AudioSystem>();
            _lastSoundTime = DateTime.UtcNow;
        }

        public override void Update(float deltaSeconds)
        {
            _particleSystem.ModifyAllParticles((ref ParticleState ps) =>
            {
                // SIMD BUG: "direction" will be incorrect if we do the "difference" calculation inside the call to Normalize.
                // https://github.com/dotnet/coreclr/issues/8220
                Vector3 targetPosition = _vacuumTarget.Position;
                Vector3 currentOffset = ps.Offset;
                Vector3 difference = targetPosition - currentOffset;
                Vector3 direction = Vector3.Normalize(difference);

                ps.Velocity = (ps.Velocity * (1 - .1f * (deltaSeconds * 60f)) + (direction * Acceleration * deltaSeconds));
                float distance = difference.Length();
                if (distance <= DeletionDistance)
                {
                    ps.Age = _particleSystem.ParticleLifetime;
                    PlayCollectedSound();
                }
            });

            if (_particleSystem.GetParticleCount() == 0)
            {
                GameObject.Destroy();
            }
        }

        private void PlayCollectedSound()
        {
            DateTime currentTime = DateTime.UtcNow;
            if ((currentTime - _lastSoundTime).TotalSeconds > _soundInterval)
            {
                float pitch = _previousPitch + (0.03f * _pitchDirection);
                if (pitch >= 2.0f)
                {
                    pitch -= (pitch - 2.0f) * 2;
                    _pitchDirection = -1f;
                }
                else if (pitch <= 0.5f)
                {
                    pitch += (0.5f - pitch) * 2;
                    _pitchDirection = 1f;
                }

                _audioSystem.PlaySound(_waveFile, 0.3f, pitch, _vacuumTarget.Position, AudioPositionKind.AbsoluteWorld);
                _lastSoundTime = currentTime;
                _previousPitch = pitch;
            }
        }
    }
}