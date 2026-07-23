using System;
using System.Globalization;
using FixedMathSharp;

namespace Combat.Core.Battle
{
    public readonly struct BattleScalar : IEquatable<BattleScalar>, IComparable<BattleScalar>
    {
        private readonly Fixed64 _value;

        private BattleScalar(Fixed64 value)
        {
            _value = value;
        }

        public static BattleScalar Zero => new BattleScalar(Fixed64.Zero);
        public static BattleScalar One => new BattleScalar(Fixed64.One);
        public static BattleScalar Epsilon => FromRaw(Fixed64.Epsilon.m_rawValue);
        public static BattleScalar TwoPi => FromRaw(Fixed64.TwoPi.m_rawValue);
        public long RawValue => _value.m_rawValue;

        public static BattleScalar FromRaw(long rawValue)
        {
            return new BattleScalar(Fixed64.FromRaw(rawValue));
        }

        public static BattleScalar FromFloat(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            return FromDouble(value);
        }

        public static BattleScalar FromDouble(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            return new BattleScalar(Fixed64.FromDouble(value));
        }

        public static BattleScalar FromInt(int value)
        {
            return new BattleScalar((Fixed64)value);
        }

        public float ToFloat()
        {
            return (float)_value;
        }

        public double ToDouble()
        {
            return (double)_value;
        }

        public int ToIntRoundHalfUpSaturating()
        {
            long raw = RawValue;
            if (raw <= 0)
            {
                return 0;
            }

            long oneRaw = One.RawValue;
            long maxRaw = (long)int.MaxValue * oneRaw;
            if (raw >= maxRaw)
            {
                return int.MaxValue;
            }

            long whole = raw / oneRaw;
            long remainder = raw % oneRaw;
            long half = (oneRaw + 1L) / 2L;
            if (remainder >= half)
            {
                whole++;
            }

            return whole >= int.MaxValue ? int.MaxValue : (int)whole;
        }

        public int CompareTo(BattleScalar other)
        {
            return _value.CompareTo(other._value);
        }

        public bool Equals(BattleScalar other)
        {
            return _value.Equals(other._value);
        }

        public override bool Equals(object obj)
        {
            return obj is BattleScalar other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public override string ToString()
        {
            return _value.ToString(null, CultureInfo.InvariantCulture);
        }

        public static BattleScalar Sqrt(BattleScalar value)
        {
            return new BattleScalar(FixedMath.Sqrt(value._value));
        }

        public static BattleScalar Sin(BattleScalar value)
        {
            return new BattleScalar(FixedMath.Sin(value._value));
        }

        public static BattleScalar Cos(BattleScalar value)
        {
            return new BattleScalar(FixedMath.Cos(value._value));
        }

        public static BattleScalar operator +(BattleScalar left, BattleScalar right)
        {
            return new BattleScalar(left._value + right._value);
        }

        public static BattleScalar operator -(BattleScalar left, BattleScalar right)
        {
            return new BattleScalar(left._value - right._value);
        }

        public static BattleScalar operator -(BattleScalar value)
        {
            return new BattleScalar(-value._value);
        }

        public static BattleScalar operator *(BattleScalar left, BattleScalar right)
        {
            return new BattleScalar(left._value * right._value);
        }

        public static BattleScalar operator /(BattleScalar left, BattleScalar right)
        {
            if (right._value == Fixed64.Zero)
            {
                throw new DivideByZeroException();
            }

            return new BattleScalar(left._value / right._value);
        }

        public static bool operator ==(BattleScalar left, BattleScalar right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BattleScalar left, BattleScalar right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(BattleScalar left, BattleScalar right)
        {
            return left._value < right._value;
        }

        public static bool operator >(BattleScalar left, BattleScalar right)
        {
            return left._value > right._value;
        }

        public static bool operator <=(BattleScalar left, BattleScalar right)
        {
            return left._value <= right._value;
        }

        public static bool operator >=(BattleScalar left, BattleScalar right)
        {
            return left._value >= right._value;
        }
    }
}
