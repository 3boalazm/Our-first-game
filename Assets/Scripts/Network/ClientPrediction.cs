using System.Collections.Generic;
using Game.Paperio;
using UnityEngine;

namespace Network
{
    public struct PendingInput
    {
        public uint Tick;
        public Direction Direction;
        public Vector2Int PredictedGridPos;
    }

    public class ClientPrediction
    {
        private readonly List<PendingInput> _pendingInputs = new();
        private Vector2Int _predictedPosition;
        private Direction _currentDirection = Direction.None;
        private uint _lastServerTick;

        private readonly uint _gridWidth;
        private readonly uint _gridHeight;

        private int _totalCorrectionCount;
        private int _significantCorrectionCount;
        private int _totalReconciliations;

        private uint _moveIntervalTicks = 3;
        private uint _moveTimer = 0;

        private const int MaxPredictionError = 3;

        public Vector2Int PredictedPosition => _predictedPosition;
        public Direction CurrentDirection => _currentDirection;
        public int PendingInputCount => _pendingInputs.Count;
        public int TotalCorrectionCount => _totalCorrectionCount;

        public ClientPrediction(uint gridWidth, uint gridHeight)
        {
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
        }

        public void SetMoveInterval(uint moveIntervalTicks)
        {
            _moveIntervalTicks = (uint)Mathf.Max(1, moveIntervalTicks);
            _moveTimer = 0;
        }

        public void Initialize(Vector2Int serverPosition, Direction direction)
        {
            _predictedPosition = serverPosition;
            _currentDirection = direction;
            _pendingInputs.Clear();
            _lastServerTick = 0;
            _totalCorrectionCount = 0;
            _totalReconciliations = 0;
            _moveTimer = 0;
        }

        public void RecordInput(Direction direction, uint estimatedTick)
        {
            _currentDirection = direction;
            _moveTimer = 0;

            _pendingInputs.Add(new PendingInput
            {
                Tick = estimatedTick,
                Direction = direction,
                PredictedGridPos = _predictedPosition
            });

            if (_pendingInputs.Count > 60)
            {
                _pendingInputs.RemoveAt(0);
            }
        }

        public void AdvancePrediction(uint estimatedTick)
        {
            if (_currentDirection == Direction.None)
            {
                return;
            }

            if (_moveTimer > 0)
            {
                _moveTimer--;
                return;
            }
            
            _moveTimer = _moveIntervalTicks - 1;

            var delta = DirectionDelta(_currentDirection);
            var newPos = new Vector2Int(
                _predictedPosition.x + delta.x,
                _predictedPosition.y + delta.y
            );

            newPos.x = (int)Mathf.Clamp(newPos.x, 0, _gridWidth - 1);
            newPos.y = (int)Mathf.Clamp(newPos.y, 0, _gridHeight - 1);

            _predictedPosition = newPos;
        }

        public bool Reconcile(uint serverTick, Vector2Int serverPosition, Direction serverDirection)
        {
            _lastServerTick = serverTick;
            _totalReconciliations++;

            _pendingInputs.RemoveAll(input => input.Tick <= serverTick);

            int errorX = Mathf.Abs(_predictedPosition.x - serverPosition.x);
            int errorY = Mathf.Abs(_predictedPosition.y - serverPosition.y);

            if (errorX == 0 && errorY == 0)
            {
                if (_currentDirection != serverDirection)
                {
                    _currentDirection = serverDirection;
                }
                return false;
            }

            int totalError = errorX + errorY;
            _totalCorrectionCount++;
            
            if (totalError > 1)
            {
                _significantCorrectionCount++;
            }

            _predictedPosition = serverPosition;
            _currentDirection = serverDirection;
            _moveTimer = 0;

            foreach (var input in _pendingInputs)
            {
                _currentDirection = input.Direction;
                var delta = DirectionDelta(_currentDirection);
                var newPos = new Vector2Int(
                    _predictedPosition.x + delta.x,
                    _predictedPosition.y + delta.y
                );
                newPos.x = (int)Mathf.Clamp(newPos.x, 0, _gridWidth - 1);
                newPos.y = (int)Mathf.Clamp(newPos.y, 0, _gridHeight - 1);
                _predictedPosition = newPos;
            }

            if (_totalReconciliations % 100 == 0)
            {
                float errorRate = (float)_totalCorrectionCount / _totalReconciliations * 100f;
                float significantRate = (float)_significantCorrectionCount / _totalReconciliations * 100f;
                
                Debug.Log($"[ClientPrediction] Stats over {_totalReconciliations} reconciliations: " +
                          $"{_totalCorrectionCount} total corrections ({errorRate:F1}%), " +
                          $"{_significantCorrectionCount} significant ({significantRate:F1}%), " +
                          $"{_pendingInputs.Count} pending inputs");
            }

            return true;
        }

        private static Vector2Int DirectionDelta(Direction dir)
        {
            return dir switch
            {
                Direction.Up => new Vector2Int(0, 1),
                Direction.Down => new Vector2Int(0, -1),
                Direction.Left => new Vector2Int(-1, 0),
                Direction.Right => new Vector2Int(1, 0),
                _ => Vector2Int.zero
            };
        }
    }
}