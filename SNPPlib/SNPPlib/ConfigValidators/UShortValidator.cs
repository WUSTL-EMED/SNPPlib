using System;
using System.Configuration;

namespace SNPPlib.ConfigValidators
{
    public class UShortValidator : ConfigurationValidatorBase
    {
        private bool Exclusive;
        private ushort MaxValue = ushort.MaxValue;
        private ushort MinValue = ushort.MinValue;

        public UShortValidator(ushort minValue, ushort maxValue) :
            this(minValue, maxValue, false)
        {
        }

        public UShortValidator(ushort minValue, ushort maxValue, bool rangeIsExclusive)
        {
            if (minValue > maxValue)
                throw new ArgumentOutOfRangeException("minValue");

            MinValue = minValue;
            MaxValue = maxValue;
            Exclusive = rangeIsExclusive;
        }

        public override bool CanValidate(Type type)
        {
            return type == typeof(ushort);
        }

        public override void Validate(object value)
        {
            if (value == null)
                return;
            if (value.GetType() != typeof(ushort))
                throw new ArgumentException(Resource.ConfigInvalidType, String.Empty);

            var val = (ushort)value;
            if (Exclusive)
            {
                if (MinValue <= val && val <= MaxValue)
                    throw new ArgumentException(Resource.ConfigNotOutsideRange, String.Empty);
            }
            else
            {
                if (MaxValue < val || val < MinValue)
                    throw new ArgumentException(Resource.ConfigOutsideRange, String.Empty);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class UShortValidatorAttribute : ConfigurationValidatorAttribute
    {
        private ushort _Max = ushort.MaxValue;
        private ushort _Min = ushort.MinValue;

        public UShortValidatorAttribute()
        {
        }

        public bool ExcludeRange { get; set; }

        public ushort MaxValue
        {
            get
            {
                return _Max;
            }
            set
            {
                if (_Min > value)
                    throw new ArgumentOutOfRangeException("value");
                _Max = value;
            }
        }

        public ushort MinValue
        {
            get
            {
                return _Min;
            }
            set
            {
                if (_Max < value)
                    throw new ArgumentOutOfRangeException("value");
                _Min = value;
            }
        }

        public override ConfigurationValidatorBase ValidatorInstance
        {
            get
            {
                return new UShortValidator(MinValue, MaxValue, ExcludeRange);
            }
        }
    }
}