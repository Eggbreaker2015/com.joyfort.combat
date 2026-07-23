using System;

namespace Combat.Core.Battle
{
    public readonly struct BattleVector2 : IEquatable<BattleVector2>
    {
        private static BattleScalar NormalizedEpsilon => BattleScalar.FromFloat(0.00001f);

        private readonly BattleScalar _x;
        private readonly BattleScalar _y;

        public BattleVector2(float x, float y)
        {
            _x = BattleScalar.FromFloat(x);
            _y = BattleScalar.FromFloat(y);
        }

        public BattleVector2(BattleScalar x, BattleScalar y)
        {
            _x = x;
            _y = y;
        }

        public BattleScalar XScalar => _x;
        public BattleScalar YScalar => _y;
        public long XRaw => _x.RawValue;
        public long YRaw => _y.RawValue;
        public float X => _x.ToFloat();
        public float Y => _y.ToFloat();

        public static BattleVector2 Zero => new BattleVector2(BattleScalar.Zero, BattleScalar.Zero);
        public static BattleVector2 Right => new BattleVector2(BattleScalar.One, BattleScalar.Zero);

        public BattleScalar SqrMagnitudeScalar => _x * _x + _y * _y;
        public BattleScalar MagnitudeScalar => BattleScalar.Sqrt(SqrMagnitudeScalar);
        public float SqrMagnitude => SqrMagnitudeScalar.ToFloat();
        public float Magnitude => MagnitudeScalar.ToFloat();

        public BattleVector2 Normalized
        {
            get
            {
                if (_y == BattleScalar.Zero)
                {
                    if (_x > NormalizedEpsilon)
                    {
                        return Right;
                    }

                    if (_x < -NormalizedEpsilon)
                    {
                        return new BattleVector2(-BattleScalar.One, BattleScalar.Zero);
                    }
                }

                if (_x == BattleScalar.Zero)
                {
                    if (_y > NormalizedEpsilon)
                    {
                        return new BattleVector2(BattleScalar.Zero, BattleScalar.One);
                    }

                    if (_y < -NormalizedEpsilon)
                    {
                        return new BattleVector2(BattleScalar.Zero, -BattleScalar.One);
                    }
                }

                BattleScalar magnitude = MagnitudeScalar;
                return magnitude <= NormalizedEpsilon ? Zero : this / magnitude;
            }
        }

        public static BattleVector2 FromRaw(long xRaw, long yRaw)
        {
            return new BattleVector2(BattleScalar.FromRaw(xRaw), BattleScalar.FromRaw(yRaw));
        }

        public static BattleVector2 operator +(BattleVector2 left, BattleVector2 right)
        {
            return new BattleVector2(left._x + right._x, left._y + right._y);
        }

        public static BattleVector2 operator -(BattleVector2 left, BattleVector2 right)
        {
            return new BattleVector2(left._x - right._x, left._y - right._y);
        }

        public static BattleVector2 operator *(BattleVector2 value, BattleScalar scalar)
        {
            return new BattleVector2(value._x * scalar, value._y * scalar);
        }

        public static BattleVector2 operator /(BattleVector2 value, BattleScalar scalar)
        {
            return new BattleVector2(value._x / scalar, value._y / scalar);
        }

        public static BattleScalar DistanceScalar(BattleVector2 a, BattleVector2 b)
        {
            return (a - b).MagnitudeScalar;
        }

        public static BattleScalar SqrDistanceScalar(BattleVector2 a, BattleVector2 b)
        {
            return (a - b).SqrMagnitudeScalar;
        }

        public static float Distance(BattleVector2 a, BattleVector2 b)
        {
            return DistanceScalar(a, b).ToFloat();
        }

        public static float SqrDistance(BattleVector2 a, BattleVector2 b)
        {
            return SqrDistanceScalar(a, b).ToFloat();
        }

        public bool Equals(BattleVector2 other)
        {
            return _x.Equals(other._x) && _y.Equals(other._y);
        }

        public override bool Equals(object obj)
        {
            return obj is BattleVector2 other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (_x, _y).GetHashCode();
        }

        public override string ToString()
        {
            return $"({X}, {Y})";
        }
    }
}
