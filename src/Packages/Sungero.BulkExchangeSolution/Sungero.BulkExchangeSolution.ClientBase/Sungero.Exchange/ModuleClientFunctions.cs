using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace Sungero.BulkExchangeSolution.Module.Exchange.Client
{
  partial class ModuleFunctions
  {

    /// <summary>
    /// 
    /// </summary>
    public virtual void SignChecked()
    {
      Sungero.BulkExchangeSolution.PublicFunctions.Module.SignCheckedDocuments();
    }

  }
}