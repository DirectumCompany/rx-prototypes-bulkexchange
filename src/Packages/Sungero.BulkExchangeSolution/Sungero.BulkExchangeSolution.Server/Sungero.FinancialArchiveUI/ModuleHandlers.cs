using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace Sungero.BulkExchangeSolution.Module.FinancialArchiveUI.Server
{
  partial class ForSignatureFolderHandlers
  {

    public virtual IQueryable<Sungero.Docflow.IAccountingDocumentBase> ForSignatureDataQuery(IQueryable<Sungero.Docflow.IAccountingDocumentBase> query)
    {
      var currentUser = Company.Employees.Current;
      var assignmentsDocuments = Docflow.ApprovalSigningAssignments.GetAll().Where(a => a.Performer == currentUser && a.Status == Docflow.ApprovalAssignment.Status.InProcess)
        .ToList()
        .SelectMany(d => d.DocumentGroup.OfficialDocuments)
        .Distinct()
        .ToList();
      var incomingExchangeDocuments = Sungero.Exchange.ExchangeDocumentInfos.GetAll()
        .Where(x => x.MessageType == ExchangeDocumentInfo.MessageType.Incoming)
        .Where(x => assignmentsDocuments.Contains(x.Document)).Select(d => d.Document).ToList();

      return query = query.Where(x => incomingExchangeDocuments.Contains(x));
    }
  }

  partial class FinancialArchiveUIHandlers
  {
  }
}