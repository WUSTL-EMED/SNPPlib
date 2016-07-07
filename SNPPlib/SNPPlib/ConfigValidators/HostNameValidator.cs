using System;
using System.Configuration;

namespace SNPPlib.ConfigValidators
{
    internal class HostnameValidator : ConfigurationValidatorBase
    {
        private static bool DefaultChecked = false;

        private bool IgnoreDefaultValue;

        public HostnameValidator(bool ignoreDefaultValue)
        {
            IgnoreDefaultValue = ignoreDefaultValue;
        }

        public override bool CanValidate(Type type)
        {
            return type == typeof(string);
        }

        public override void Validate(object value)
        {
            //This is gross but there are problems trying to get a null default value.
            if (IgnoreDefaultValue && !DefaultChecked)
            {
                DefaultChecked = true;
                return;
            }

            if (value == null)
                return;
            if (value.GetType() != typeof(string))
                throw new ArgumentException(Resource.ConfigInvalidType, String.Empty);

            var val = (string)value;
            if (Uri.CheckHostName(val) == UriHostNameType.Unknown)
                throw new ArgumentException(Resource.ConfigInvalidHostName, String.Empty);
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class HostnameValidatorAttribute : ConfigurationValidatorAttribute
    {
        public HostnameValidatorAttribute()
        {
        }

        public bool IgnoreDefaultValue { get; set; }

        public override ConfigurationValidatorBase ValidatorInstance
        {
            get
            {
                return new HostnameValidator(IgnoreDefaultValue);
            }
        }
    }
}