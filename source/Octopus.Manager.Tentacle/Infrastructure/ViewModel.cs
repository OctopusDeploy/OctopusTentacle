using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using FluentValidation;
using FluentValidation.Internal;
using Octopus.Manager.Tentacle.Annotations;

namespace Octopus.Manager.Tentacle.Infrastructure
{
    public class ViewModel : IDataErrorInfo, INotifyPropertyChanged
    {
        private readonly HashSet<string> activeRuleSets = new HashSet<string>();
        private readonly IDictionary<string, string> errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private bool isValid;

        public ViewModel()
        {
            isValid = true;
        }

        public IValidator Validator { get; protected set; }

        public string this[string columnName] => errors.ContainsKey(columnName) ? errors[columnName] : null;

        string IDataErrorInfo.Error => "";

        public bool IsValid
        {
            get => isValid;
            private set
            {
                if (value.Equals(isValid)) return;
                isValid = value;
                OnPropertyChanged();
            }
        }

        public void PushRuleSet(string ruleSet)
        {
            if (ruleSet == null) return;
            activeRuleSets.Add(ruleSet);
            Validate();
        }

        public void PopRuleSet(string ruleSet)
        {
            if (ruleSet == null) return;
            if (!activeRuleSets.Contains(ruleSet)) return;
            activeRuleSets.Remove(ruleSet);
            Validate();
        }

        public void Validate()
        {
            if (Validator != null)
            {
                var validationResult = Validator.Validate(new ValidationContext(this, new PropertyChain(), new RulesetValidatorSelector(activeRuleSets.ToArray())));

                var errorsByProperty = validationResult.Errors.GroupBy(g => g.PropertyName);
                errors.Clear();
                foreach (var errorByProperty in errorsByProperty)
                {
                    var errorMessage = new StringBuilder();
                    foreach (var error in errorByProperty) errorMessage.AppendLine(error.ErrorMessage);

                    errors[errorByProperty.Key] = errorMessage.ToString().Trim();
                }

                IsValid = validationResult.IsValid;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            if (propertyName != "IsValid") Validate();

            var handler = PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}