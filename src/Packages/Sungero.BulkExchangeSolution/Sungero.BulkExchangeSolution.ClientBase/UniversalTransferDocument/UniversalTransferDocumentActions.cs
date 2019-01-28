using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.BulkExchangeSolution.UniversalTransferDocument;

namespace Sungero.BulkExchangeSolution.Client
{
  partial class UniversalTransferDocumentActions
  {
    public virtual void Reject(Sungero.Domain.Client.ExecuteActionArgs e)
    {
      
    }

    public virtual bool CanReject(Sungero.Domain.Client.CanExecuteActionArgs e)
    {
      return true;
    }

  }

}