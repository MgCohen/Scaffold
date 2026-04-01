using UnityEngine;
using Scaffold.Navigation.Contracts;
using Scaffold.MVVM.Binding;
using Scaffold.MVVM.Contracts;
using System.Collections.Generic;
using System;
namespace Scaffold.MVVM
{
    public enum EventLedgerExceptionMode
    {
        ReportAndContinue = 0,
        ThrowAfterDispatch = 1,
    }
}



