using Game.Paperio;
using UnityEngine;

namespace Network
{
    public struct PlayerSnapshot
    {
        public uint Tick;
        public Vector3 WorldPosition;
        public Direction Direction;
        public bool Alive;
        public bool IsValid;

        public static PlayerSnapshot Invalid => new() { IsValid = false };
    }

    public struct InterpolationResult
    {
        public Vector3 Position;
        public Direction Direction;
        public bool Alive;
        public bool IsExtrapolating;
        public bool IsValid;

        public static InterpolationResult Invalid => new() { IsValid = false };
    }
    
    public class InterpolationBuffer
    {
        private readonly PlayerSnapshot[] _buffer;
        private int _writeIndex;
        private int _count;

        private uint _latestTick;

        private readonly float _renderDelayTicks;

        private readonly float _maxExtrapolationTicks;

        private readonly float _tickDurationSeconds;

        public InterpolationBuffer(
            int bufferSize = 10,
            float renderDelayTicks = 1f,
            float maxExtrapolationTicks = 3f,
            float tickDurationSeconds = 0.05f)
        {
            _buffer = new PlayerSnapshot[bufferSize];
            _renderDelayTicks = renderDelayTicks;
            _maxExtrapolationTicks = maxExtrapolationTicks;
            _tickDurationSeconds = tickDurationSeconds;
            _writeIndex = 0;
            _count = 0;
            _latestTick = 0;
        }

        public uint LatestTick => _latestTick;

        public int Count => _count;

        public void AddSnapshot(uint tick, Vector3 worldPosition, Direction direction, bool alive)
        {
            if (tick <= _latestTick && _count > 0)
            {
                return;
            }

            var snapshot = new PlayerSnapshot
            {
                Tick = tick,
                WorldPosition = worldPosition,
                Direction = direction,
                Alive = alive,
                IsValid = true
            };

            _buffer[_writeIndex] = snapshot;
            _writeIndex = (_writeIndex + 1) % _buffer.Length;
            _count = Mathf.Min(_count + 1, _buffer.Length);
            _latestTick = tick;
        }

        public InterpolationResult Sample(float tickProgress)
        {
            if (_count < 2)
            {
                if (_count == 1)
                {
                    var only = GetSnapshot(0);
                    return new InterpolationResult
                    {
                        Position = only.WorldPosition,
                        Direction = only.Direction,
                        Alive = only.Alive,
                        IsExtrapolating = false,
                        IsValid = true
                    };
                }

                return InterpolationResult.Invalid;
            }

            float renderTick = _latestTick - _renderDelayTicks + tickProgress;

            PlayerSnapshot before = PlayerSnapshot.Invalid;
            PlayerSnapshot after = PlayerSnapshot.Invalid;

            for (int i = 0; i < _count; i++)
            {
                var snap = GetSnapshot(i);

                if (snap.Tick <= renderTick)
                {
                    if (!before.IsValid || snap.Tick > before.Tick)
                    {
                        before = snap;
                    }
                }

                if (snap.Tick > renderTick)
                {
                    if (!after.IsValid || snap.Tick < after.Tick)
                    {
                        after = snap;
                    }
                }
            }

            if (before.IsValid && after.IsValid)
            {
                float span = after.Tick - before.Tick;
                float t = span > 0 ? (renderTick - before.Tick) / span : 0f;
                t = Mathf.Clamp01(t);

                return new InterpolationResult
                {
                    Position = Vector3.Lerp(before.WorldPosition, after.WorldPosition, t),
                    Direction = t < 0.5f ? before.Direction : after.Direction,
                    Alive = after.Alive,
                    IsExtrapolating = false,
                    IsValid = true
                };
            }

            if (before.IsValid && !after.IsValid)
            {
                float ticksPastLast = renderTick - before.Tick;

                if (ticksPastLast > _maxExtrapolationTicks)
                {
                    return new InterpolationResult
                    {
                        Position = before.WorldPosition,
                        Direction = before.Direction,
                        Alive = before.Alive,
                        IsExtrapolating = true,
                        IsValid = true
                    };
                }

                Vector3 velocity = EstimateVelocity(before);
                float extrapolationTime = ticksPastLast * _tickDurationSeconds;

                return new InterpolationResult
                {
                    Position = before.WorldPosition + velocity * extrapolationTime,
                    Direction = before.Direction,
                    Alive = before.Alive,
                    IsExtrapolating = true,
                    IsValid = true
                };
            }

            if (!before.IsValid && after.IsValid)
            {
                return new InterpolationResult
                {
                    Position = after.WorldPosition,
                    Direction = after.Direction,
                    Alive = after.Alive,
                    IsExtrapolating = false,
                    IsValid = true
                };
            }

            return InterpolationResult.Invalid;
        }

        private Vector3 EstimateVelocity(PlayerSnapshot latest)
        {
            PlayerSnapshot previous = PlayerSnapshot.Invalid;

            for (int i = 0; i < _count; i++)
            {
                var snap = GetSnapshot(i);
                if (snap.Tick < latest.Tick)
                {
                    if (!previous.IsValid || snap.Tick > previous.Tick)
                    {
                        previous = snap;
                    }
                }
            }

            if (!previous.IsValid || previous.Tick == latest.Tick)
            {
                return Vector3.zero;
            }

            float tickDelta = latest.Tick - previous.Tick;
            float timeDelta = tickDelta * _tickDurationSeconds;

            if (timeDelta <= 0)
            {
                return Vector3.zero;
            }

            return (latest.WorldPosition - previous.WorldPosition) / timeDelta;
        }

        private PlayerSnapshot GetSnapshot(int reverseIndex)
        {
            if (reverseIndex >= _count) return PlayerSnapshot.Invalid;
            int idx = (_writeIndex - 1 - reverseIndex + _buffer.Length * 2) % _buffer.Length;
            return _buffer[idx];
        }

        public void Clear()
        {
            _count = 0;
            _writeIndex = 0;
            _latestTick = 0;
        }
    }
}