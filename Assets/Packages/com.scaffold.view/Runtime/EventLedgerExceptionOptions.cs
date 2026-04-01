using UnityEngine;
using Scaffold.Navigation.Contracts;
using Scaffold.MVVM.Binding;
using Scaffold.MVVM.Contracts;
using System.Collections.Generic;
using System;

namespace Scaffold.MVVM
{
    public sealed class EventLedgerExceptionOptions
    {
        public EventLedgerExceptionOptions(EventLedgerExceptionMode mode, Action<Exception, Type> reporter)
        {
            ValidateMode(mode);
            Mode = mode;
            Reporter = reporter;
        }

        public EventLedgerExceptionMode Mode { get; }
        public Action<Exception, Type> Reporter { get; }

        private void ValidateMode(EventLedgerExceptionMode mode)
        {
            if (!Enum.IsDefined(typeof(EventLedgerExceptionMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }
    }
}



