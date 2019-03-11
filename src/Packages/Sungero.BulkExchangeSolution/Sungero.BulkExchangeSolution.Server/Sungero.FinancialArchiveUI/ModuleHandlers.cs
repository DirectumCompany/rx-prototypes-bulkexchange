using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace Sungero.BulkExchangeSolution.Module.FinancialArchiveUI.Server
{
  partial class OutgoingSentDocumentsFolderHandlers
  {

    public virtual IQueryable<Sungero.Docflow.IOfficialDocument> OutgoingSentDocumentsDataQuery(IQueryable<Sungero.Docflow.IOfficialDocument> query)
    {
      var infos = ExchangeDocumentInfos
        .GetAll(d => d.MessageType == ExchangeDocumentInfo.MessageType.Outgoing);
      
      query = query.Where(d => infos.Select(i => i.Document).Contains(d));
      
      if (_filter == null)
        return query;
      
      #region Фильтры

      // Фильтр "Наша организация".
      if (_filter.BusinessUnit != null)
        query = query.Where(d => Equals(d.BusinessUnit, _filter.BusinessUnit));
      
      // Фильтр "Подразделение".
      if (_filter.Department != null)
        query = query.Where(d => Equals(d.Department, _filter.Department));
      
      // Фильтр "Ответственный"
      if (_filter.Responsible != null)
        query = query.Where(d => Docflow.AccountingDocumentBases.Is(d) && Equals(Docflow.AccountingDocumentBases.As(d).ResponsibleEmployee, _filter.Responsible) ||
                            Docflow.ContractualDocumentBases.Is(d) && Equals(Sungero.Contracts.ContractualDocuments.As(d).ResponsibleEmployee, _filter.Responsible));

      // Фильтр "Контрагент".
      if (_filter.Counterparty != null)
        infos = infos.Where(i => Equals(i.Counterparty, _filter.Counterparty));

      // Фильтр "Тип документа"
      if (_filter.Accounting || _filter.Contractual || _filter.Other)
        query = query.Where(d => (_filter.Accounting && Docflow.AccountingDocumentBases.Is(d)) ||
                            (_filter.Contractual && Docflow.ContractualDocumentBases.Is(d)) ||
                            (_filter.Other && !Docflow.AccountingDocumentBases.Is(d) && !Docflow.ContractualDocumentBases.Is(d)));
      
      #region Фильтрация по дате отправки

      DateTime? beginDate = null;
      DateTime? endDate = null;
      var currentDate = Calendar.UserToday;
      
      if (_filter.Today)
      {
        beginDate = currentDate;
        endDate = currentDate;
      }

      if (_filter.LastThreeDays)
      {
        beginDate = currentDate.AddDays(-3);
        endDate = currentDate;
      }
      if (_filter.LastWeek)
      {
        beginDate = currentDate.AddDays(-7);
        endDate = currentDate;
      }
      if (_filter.ManualPeriod)
      {
        if (_filter.DateRangeFrom.HasValue)
          beginDate = _filter.DateRangeFrom.Value;
        if (_filter.DateRangeTo.HasValue)
          endDate = _filter.DateRangeTo.Value;
      }

      if (beginDate != null)
        infos = infos.Where(d => d.MessageDate >= beginDate.Value.BeginningOfDay());
      
      if (endDate != null)
        infos = infos.Where(d => d.MessageDate <= endDate.Value.EndOfDay());
      
      #endregion
      
      #endregion
      
      return query.Where(d => infos.Select(i => i.Document).Contains(d));
    }
  }

  partial class OutgoingDocumentToSendFolderHandlers
  {

    public virtual IQueryable<Sungero.Docflow.IOfficialDocument> OutgoingDocumentToSendDataQuery(IQueryable<Sungero.Docflow.IOfficialDocument> query)
    {
      query = query.Where(d => d.ExchangeState == null && d.LastVersionApproved == true && d.ExternalApprovalState == null &&
                          (Docflow.AccountingDocumentBases.Is(d) && Docflow.AccountingDocumentBases.As(d).Counterparty.CanExchange == true ||
                           Docflow.ContractualDocumentBases.Is(d) && Sungero.Contracts.ContractualDocuments.As(d).Counterparty.CanExchange == true));
      
      if (_filter == null)
        return query;
      
      #region Фильтры
      
      // Фильтр "Наша организация".
      if (_filter.BusinessUnit != null)
        query = query.Where(d => Equals(d.BusinessUnit, _filter.BusinessUnit));
      
      // Фильтр "Подразделение".
      if (_filter.Department != null)
        query = query.Where(d => Equals(d.Department, _filter.Department));
      
      // Фильтр "Ответственный"
      if (_filter.Responsible != null)
        query = query.Where(d => Docflow.AccountingDocumentBases.Is(d) && Equals(Docflow.AccountingDocumentBases.As(d).ResponsibleEmployee, _filter.Responsible) ||
                            Docflow.ContractualDocumentBases.Is(d) && Equals(Sungero.Contracts.ContractualDocuments.As(d).ResponsibleEmployee, _filter.Responsible));
      
      // Фильтр "Контрагент".
      if (_filter.Counterparty != null)
        query = query.Where(d => Docflow.AccountingDocumentBases.Is(d) && Equals(Docflow.AccountingDocumentBases.As(d).Counterparty, _filter.Counterparty) ||
                            Docflow.ContractualDocumentBases.Is(d) && Equals(Sungero.Contracts.ContractualDocuments.As(d).Counterparty, _filter.Counterparty));
      
      // Фильтр "Тип документа"
      if (_filter.Accounting || _filter.Contractual)
        query = query.Where(d => (_filter.Accounting && Docflow.AccountingDocumentBases.Is(d)) ||
                            (_filter.Contractual && Docflow.ContractualDocumentBases.Is(d)));
      #endregion
      
      return query;
    }
  }

  partial class VerifiedOrNeedVerifyDocumentsFolderHandlers
  {

    public virtual IQueryable<Sungero.Docflow.IAccountingDocumentBase> VerifiedOrNeedVerifyDocumentsDataQuery(IQueryable<Sungero.Docflow.IAccountingDocumentBase> query)
    {
      var infos = ExchangeDocumentInfos
        .GetAll(d => (d.VerificationStatus == Sungero.BulkExchangeSolution.ExchangeDocumentInfo.VerificationStatus.Required ||
                      d.VerificationStatus == Sungero.BulkExchangeSolution.ExchangeDocumentInfo.VerificationStatus.Completed) && d.PurchaseOrder != null);
      
      if (_filter == null)
        return query.Where(d => infos.Select(i => i.Document).Contains(d));

      #region Фильтры

      // Фильтр "Ожидает проверки".
      if (_filter.Required && !_filter.Verified)
        infos = infos.Where(d => d.VerificationStatus == Sungero.BulkExchangeSolution.ExchangeDocumentInfo.VerificationStatus.Required);
      
      // Фильтр "Прошли проверку".
      if (_filter.Verified && !_filter.Required)
        infos = infos.Where(d => d.VerificationStatus == Sungero.BulkExchangeSolution.ExchangeDocumentInfo.VerificationStatus.Completed);

      // Фильтр "Наша организация".
      query = query.Where(d => infos.Select(i => i.Document).Contains(d));
      if (_filter.BusinessUnit != null)
        query = query.Where(x => Equals(x.BusinessUnit, _filter.BusinessUnit));
      
      // Фильтр "Подразделение".
      if (_filter.Department != null)
        query = query.Where(x => Equals(x.Department, _filter.Department));
      
      // Фильтр "Ответсвенный".
      if (_filter.Responsible != null)
        query = query.Where(x => Equals(x.ResponsibleEmployee, _filter.Responsible));

      // Фильтр "Контрагент".
      if (_filter.Counterparty != null)
        query = query.Where(x => Equals(x.Counterparty, _filter.Counterparty));
      
      #endregion     
      return query.Where(d => infos.Select(i => i.Document).Contains(d));
    }
  }

  partial class IncomingDocumentsFromServiceFolderHandlers
  {

    public virtual IQueryable<Sungero.Docflow.IOfficialDocument> IncomingDocumentsFromServiceDataQuery(IQueryable<Sungero.Docflow.IOfficialDocument> query)
    {
      var infos = Sungero.Exchange.ExchangeDocumentInfos.GetAll(i => i.MessageType == Sungero.Exchange.ExchangeDocumentInfo.MessageType.Incoming);
      
      if (_filter == null)
        return query.Where(d => infos.Select(i => i.Document).Contains(d));
      
      #region Фильтры
      
      // Фильтр "Наша организация".
      if (_filter.BusinessUnit != null)
        query = query.Where(d => Equals(d.BusinessUnit, _filter.BusinessUnit));
      
      // Фильтр "Подразделение".
      if (_filter.Department != null)
        query = query.Where(d => Equals(d.Department, _filter.Department));
      
      // Фильтр "Ответсвенный".
      if (_filter.Responsible != null)
        query = query.Where(d => Docflow.AccountingDocumentBases.Is(d) && Equals(Docflow.AccountingDocumentBases.As(d).ResponsibleEmployee, _filter.Responsible) ||
                            Docflow.ContractualDocumentBases.Is(d) && Equals(Sungero.Contracts.ContractualDocuments.As(d).ResponsibleEmployee, _filter.Responsible));
      
      // Фильтр "Контрагент".
      if (_filter.Counterparty != null)
        infos = infos.Where(i => Equals(i.Counterparty, _filter.Counterparty));
      
      #region Фильтрация по дате договора

      DateTime? beginDate = null;
      DateTime? endDate = null;
      var currentDate = Calendar.UserToday;
      
      if (_filter.Today)
      {
        beginDate = currentDate;
        endDate = currentDate;
      }

      if (_filter.ThreeDays)
      {
        beginDate = currentDate.AddDays(-1);
        endDate = currentDate.AddDays(1);
      }
      if (_filter.Week)
      {
        beginDate = currentDate.BeginningOfWeek();
        endDate = currentDate.EndOfWeek();
      }
      if (_filter.ManualPeriod)
      {
        if (_filter.DateRangeFrom.HasValue)
          beginDate = _filter.DateRangeFrom.Value;
        if (_filter.DateRangeTo.HasValue)
          endDate = _filter.DateRangeTo.Value;
      }

      if (beginDate != null)
        query = query.Where(d => d.Created >= beginDate.Value.BeginningOfDay());
      
      if (endDate != null)
        query = query.Where(d => d.Created <= endDate.Value.EndOfDay());
      
      #endregion
      
      // Фильтр "Тип документа"
      if (_filter.Accounting || _filter.Contractual || _filter.Other)
        query = query.Where(d => (_filter.Accounting && Docflow.AccountingDocumentBases.Is(d)) ||
                            (_filter.Contractual && Docflow.ContractualDocumentBases.Is(d)) ||
                            (_filter.Other && !Docflow.AccountingDocumentBases.Is(d) && !Docflow.ContractualDocumentBases.Is(d)));
      #endregion
      
      return query.Where(d => infos.Select(i => i.Document).Contains(d));
    }
  }

  partial class OutgoingDocumentsForSignatureFolderHandlers
  {

    public virtual IQueryable<Sungero.Docflow.IAccountingDocumentBase> OutgoingDocumentsForSignatureDataQuery(IQueryable<Sungero.Docflow.IAccountingDocumentBase> query)
    {
      var currentUser = Company.Employees.Current;
      var assignmentsDocuments = Docflow.ApprovalSigningAssignments.GetAll().Where(a => a.Performer == currentUser && a.Status == Docflow.ApprovalAssignment.Status.InProcess)
        .ToList()
        .SelectMany(d => d.DocumentGroup.OfficialDocuments)
        .Distinct()
        .ToList();
      var exchangeDocuments = Sungero.Exchange.ExchangeDocumentInfos.GetAll(i => assignmentsDocuments.Contains(i.Document)).Select(d => d.Document).ToList();
      
      return query = query.Where(x => assignmentsDocuments.Contains(x) && !exchangeDocuments.Contains(x));
    }
  }

  partial class IncomingDocumentsForSignatureFolderHandlers
  {

    public virtual IQueryable<Sungero.Docflow.IAccountingDocumentBase> IncomingDocumentsForSignatureDataQuery(IQueryable<Sungero.Docflow.IAccountingDocumentBase> query)
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