﻿using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.BulkExchangeSolution.ExchangeDocumentInfo;
using Sungero.Company;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.Docflow;
using Sungero.Domain.Shared;
using Sungero.Metadata;
using Status = Sungero.Workflow.AssignmentBase.Status;

namespace Sungero.BulkExchangeSolution.Server
{
  public class ModuleFunctions
  {
    /// <summary>
    /// Обработать импортированный документ.
    /// </summary>
    /// <param name="document">Документ.</param>
    /// <param name="responsible">Ответственный.</param>
    /// <param name="caseFile">Дело.</param>
    [Remote]
    public virtual void ProcessImportedDocument(IAccountingDocumentBase document, IEmployee responsible, ICaseFile caseFile)
    {
      Docflow.PublicFunctions.AccountingDocumentBase.Remote.GenerateDefaultSellerTitle(document, responsible);
      BulkExchangeSolution.Module.Exchange.PublicFunctions.Module.SetDocumentResponsible(document, responsible);
      if (caseFile != null)
        document.CaseFile = caseFile;
    }
    
    /// <summary>
    /// Получить номер заказа из xml файла формализованного документа.
    /// </summary>
    /// <param name="file">Xml файл</param>
    [Remote]
    public virtual string GetPurchaseNumber(Docflow.Structures.Module.IByteArray file)
    {
      var additionalProperties = BulkExchangeSolution.Module.Exchange.PublicFunctions.Module.GetAdditionalProperties(file.Bytes);
      
      if (additionalProperties.Any())
        return BulkExchangeSolution.Module.Exchange.PublicFunctions.Module.GetPurchaseOrderNumber(additionalProperties);

      return string.Empty;
    }
    
    /// <summary>
    /// Получить ответственного для импортируемых документов.
    /// </summary>
    /// <returns>Ответственный.</returns>
    [Remote]
    public IEmployee GetImportedDocumentsResponsible()
    {
      var accountingChiefRole = Roles.GetAll(r => r.Name.Equals(BulkExchangeSolution.Module.Exchange.Constants.Module.ChiefAccountantRoleName)).FirstOrDefault();
      var chiefAccount = Employees.As(accountingChiefRole.RecipientLinks.FirstOrDefault().Member);
      return chiefAccount;
    }
    
    /// <summary>
    /// Получить дело по умолчанию для импортируемых документов.
    /// </summary>
    /// <returns>Дело.</returns>
    [Remote]
    public ICaseFile GetImportedDocumentsDefaultCaseFile()
    {
      return Docflow.CaseFiles.GetAll(c => c.Status == Docflow.CaseFile.Status.Active).FirstOrDefault();
    }
    
    /// <summary>
    /// Получить ящик по умолчанию для нашей организации.
    /// </summary>
    /// <param name="businessUnit">Наша организация.</param>
    /// <returns>Ящик НОР.</returns>
    [Remote]
    public ExchangeCore.IBusinessUnitBox GetDefaultBox(Company.IBusinessUnit businessUnit)
    {
      var defaultService = ExchangeCore.ExchangeServices.GetAll(x => x.ExchangeProvider == ExchangeCore.ExchangeService.ExchangeProvider.Synerdocs).FirstOrDefault();
      return ExchangeCore.BusinessUnitBoxes.GetAll(x => Equals(x.BusinessUnit, businessUnit) && Equals(x.ExchangeService, defaultService)).FirstOrDefault();
    }
    
    [Remote(IsPure = true)]
    public IQueryable<IExchangeDocumentInfo> GetRejectedDocumentInfos()
    {
      return ExchangeDocumentInfos.GetAll(x => x.RejectionStatus == RejectionStatus.Required);
    }
    
    /// <summary>
    /// Получить комплекты документов требующие подписания и прошедщие сверку.
    /// </summary>
    /// <returns>Список комплектов документов.</returns>
    [Remote]
    public virtual List<Sungero.BulkExchangeSolution.Structures.Exchange.ExchangeDocumentInfo.DocumentSet> GetVerifiedSets()
    {
      var boxes = Sungero.ExchangeCore.PublicFunctions.BusinessUnitBox.Remote.GetConnectedBoxes().Where(c => c.HasExchangeServiceCertificates == true &&
                                                                                                        c.ExchangeServiceCertificates.Any(x => Equals(x.Certificate.Owner, Users.Current) &&
                                                                                                                                          x.Certificate.Enabled == true)).ToList();
      var infos = ExchangeDocumentInfos.GetAll()
        .Where(x => boxes.Contains(x.RootBox) && x.ExchangeState == Sungero.BulkExchangeSolution.ExchangeDocumentInfo.ExchangeState.SignRequired &&
               (x.SignStatus == null || x.SignStatus != SignStatus.Signed) && x.VerificationStatus == Sungero.BulkExchangeSolution.ExchangeDocumentInfo.VerificationStatus.Completed);
      
      return Sungero.BulkExchangeSolution.Functions.ExchangeDocumentInfo.GetDocumentSets(infos.ToList()).Where(x => x.IsFullSet).ToList();
    }
    
    [Remote]
    public IQueryable<IApprovalSigningAssignment> GetApprovalSigningAssignments(IAccountingDocumentBase document)
    {
      var typeGuid = document.GetEntityMetadata().GetOriginal().NameGuid;
      var groupId = Docflow.PublicConstants.Module.TaskMainGroup.ApprovalTask;
      return ApprovalSigningAssignments
        .GetAll(a => Equals(a.Performer, Users.Current) && a.Status == Status.InProcess)
        .Where(a => a.MainTask.AttachmentDetails.Any(d => d.EntityTypeGuid == typeGuid &&
                                                     d.GroupId == groupId &&
                                                     d.AttachmentId == document.Id));
    }
  }
}