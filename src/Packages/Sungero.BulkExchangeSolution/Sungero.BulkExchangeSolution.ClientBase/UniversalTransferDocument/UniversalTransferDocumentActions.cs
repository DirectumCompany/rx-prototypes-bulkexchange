using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.BulkExchangeSolution.UniversalTransferDocument;
using Sungero.Core;
using Sungero.CoreEntities;

namespace Sungero.BulkExchangeSolution.Client
{
  partial class UniversalTransferDocumentCollectionActions
  {

    public virtual bool CanSignDocumentSet(Sungero.Domain.Client.CanExecuteActionArgs e)
    {
      return true;
    }

    public virtual void SignDocumentSet(Sungero.Domain.Client.ExecuteActionArgs e)
    {
      
    }
  }

  partial class UniversalTransferDocumentActions
  {
    public virtual void ShowApprovalSigningAssignment(Sungero.Domain.Client.ExecuteActionArgs e)
    {
      Functions.Module.ShowApprovalSigningAssignments(_obj);
    }

    public virtual bool CanShowApprovalSigningAssignment(Sungero.Domain.Client.CanExecuteActionArgs e)
    {
      return true;
    }


    public virtual void Reject(Sungero.Domain.Client.ExecuteActionArgs e)
    {
      Functions.Module.RejectDocument(_obj);
    }

    public virtual bool CanReject(Sungero.Domain.Client.CanExecuteActionArgs e)
    {
      return true;
    }

  }

}