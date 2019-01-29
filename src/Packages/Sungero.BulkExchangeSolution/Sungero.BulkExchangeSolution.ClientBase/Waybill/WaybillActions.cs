using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.BulkExchangeSolution.Waybill;
using Sungero.Core;
using Sungero.CoreEntities;

namespace Sungero.BulkExchangeSolution.Client
{
  partial class WaybillCollectionActions
  {

    public virtual bool CanSignDocumentSet(Sungero.Domain.Client.CanExecuteActionArgs e)
    {
      return true;
    }

    public virtual void SignDocumentSet(Sungero.Domain.Client.ExecuteActionArgs e)
    {
      Functions.Module.SignDocuments(_objs, e);
    }
  }

  partial class WaybillActions
  {
    public virtual void ShowSet(Sungero.Domain.Client.ExecuteActionArgs e)
    {
      Functions.Module.ShowSet(_obj);
    }

    public virtual bool CanShowSet(Sungero.Domain.Client.CanExecuteActionArgs e)
    {
      return true;
    }

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