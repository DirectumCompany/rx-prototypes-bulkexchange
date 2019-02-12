using System;
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
    /// Получить номер заказа из документа.
    /// </summary>
    /// <param name="document">Документ.</param>
    /// <returns>Номер заказа.</returns>
    [Remote]
    public virtual string GetPurchaseNumber(IOfficialDocument document)
    {
      byte[] content;
      using (var memory = new System.IO.MemoryStream())
      {
        using (var sourceStream = document.LastVersion.Body.Read())
          sourceStream.CopyTo(memory);
        content = memory.ToArray();
      }
      var byteArray = Docflow.Structures.Module.ByteArray.Create(content);
      return Functions.Module.GetPurchaseNumber(byteArray);
    }
    
    /// <summary>
    /// Получить импортированные документы.
    /// </summary>
    /// <returns>Список импортированных документов.</returns>
    [Remote]
    public virtual IQueryable<IAccountingDocumentBase> GetImportedDocuments()
    {
      return Docflow.AccountingDocumentBases.GetAll(d => d.BusinessUnitBox != null &&
                                                    d.ExchangeState == null &&
                                                    (d.FormalizedFunction == Docflow.AccountingDocumentBase.FormalizedFunction.Dop ||
                                                     d.FormalizedFunction == Docflow.AccountingDocumentBase.FormalizedFunction.SchfDop));
    }
    
    /// <summary>
    /// Обработать импортированный документ.
    /// </summary>
    /// <param name="document">Документ.</param>
    /// <param name="responsible">Ответственный.</param>
    /// <param name="isFormalized">Признак что документ формализованный.</param>
    [Remote]
    public virtual void ProcessImportedDocument(IAccountingDocumentBase document, IEmployee responsible, bool isFormalized)
    {
      if (isFormalized)
        Docflow.PublicFunctions.AccountingDocumentBase.Remote.GenerateDefaultSellerTitle(document, responsible);

      BulkExchangeSolution.Module.Exchange.PublicFunctions.Module.SetDocumentResponsible(document, responsible);
      var caseFile = this.GetImportedDocumentsDefaultCaseFile();
      if (caseFile != null)
        document.CaseFile = caseFile;
      document.Save();
    }
    
    /// <summary>
    /// Получить номер заказа из xml файла формализованного документа.
    /// </summary>
    /// <param name="file">Xml файл.</param>
    /// <returns>Номер заказа.</returns>
    [Remote]
    public virtual string GetPurchaseNumber(Docflow.Structures.Module.IByteArray file)
    {
      var additionalProperties = BulkExchangeSolution.Module.Exchange.PublicFunctions.Module.GetAdditionalProperties(file.Bytes);
      
      if (additionalProperties.Any())
        return BulkExchangeSolution.Module.Exchange.PublicFunctions.Module.GetPurchaseOrderNumber(additionalProperties);

      return string.Empty;
    }
    
    /// <summary>
    /// Получить номер договора из xml файла формализованного документа.
    /// </summary>
    /// <param name="file">Xml файл.</param>
    /// <returns>Номер договора.</returns>
    [Remote]
    public virtual string GetContractNumber(Docflow.Structures.Module.IByteArray file)
    {
      var additionalProperties = BulkExchangeSolution.Module.Exchange.PublicFunctions.Module.GetAdditionalProperties(file.Bytes);
      
      if (additionalProperties.Any())
        return BulkExchangeSolution.Module.Exchange.PublicFunctions.Module.GetContractNumber(additionalProperties);

      return string.Empty;
    }
    
    /// <summary>
    /// Получить ответственного для импортируемых товарных документов.
    /// </summary>
    /// <returns>Ответственный.</returns>
    [Remote]
    public IEmployee GetImportedWaybillDocumentsResponsible()
    {
      var accountingChiefRole = Roles.GetAll(r => r.Name.Equals(BulkExchangeSolution.Module.Exchange.Constants.Module.ChiefAccountantRoleName)).FirstOrDefault();
      var chiefAccount = Employees.As(accountingChiefRole.RecipientLinks.FirstOrDefault().Member);
      return chiefAccount;
    }
    
    /// <summary>
    /// Получить ответственного по подразделению и названию должности.
    /// </summary>
    /// <param name="department">Подразделение.</param>
    /// <param name="jobTitle">Должность.</param>
    /// <returns>Ответственный.</returns>
    [Remote]
    public IEmployee GetEmployeeByJobTitle(IDepartment department, string jobTitle)
    {
      return Employees.GetAll(e => e.Department == department).Where(e => Equals(e.JobTitle.Name, jobTitle)).FirstOrDefault();
    }
    
    /// <summary>
    /// Получить подразделение по НОР и названию подразделения.
    /// </summary>
    /// <param name="businessUnit">Наша организация.</param>
    /// <param name="department">Наименование подразделения.</param>
    /// <returns>Подразделение.</returns>
    [Remote]
    public IDepartment GetDepartmentByName(IBusinessUnit businessUnit, string department)
    {
      return Sungero.Company.Departments.GetAll(d => d.BusinessUnit == businessUnit).Where(d => Equals(d.Name, department)).FirstOrDefault();
    }
    
    /// <summary>
    /// Получить договор по регистрационному номеру.
    /// </summary>
    /// <param name="contractNumber">Номер договора.</param>
    /// <returns>Договор.</returns>
    [Remote]
    public Contracts.IContract GetContractByNumber(string contractNumber)
    {
      return Sungero.Contracts.Contracts.GetAll(c => Equals(c.RegistrationNumber, contractNumber)).FirstOrDefault();
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
    
    /// <summary>
    /// Создать акт к договорному документу.
    /// </summary>
    /// <returns>Созданный акт.</returns>
    [Remote]
    public Sungero.FinancialArchive.IContractStatement CreateContractStatement()
    {
      return Sungero.FinancialArchive.ContractStatements.Create();
    }
    
    [Remote]
    public Sungero.Parties.ICounterparty GetRandomCounterParty()
    {
      return Sungero.Parties.Counterparties.GetAll().FirstOrDefault();
    }
  }
}