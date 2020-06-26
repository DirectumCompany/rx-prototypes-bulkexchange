using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Sungero.BulkExchangeSolution.ExchangeDocumentInfo;
using Sungero.Company;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.Docflow;
using Sungero.Domain.Shared;
using Sungero.Metadata;
using Sungero.SmartProcessing;
using Sungero.SmartProcessing.Structures.Module;
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
    /// <param name="titleSignatory">Подписывающий титул продавца.</param>
    /// <param name="isContractStatement">Признак нетоварного потока.</param>
    [Remote]
    public virtual void ProcessImportedDocument(IAccountingDocumentBase document, IEmployee responsible, IEmployee titleSignatory, bool isContractStatement)
    {
      if (titleSignatory != null)
        Docflow.PublicFunctions.AccountingDocumentBase.Remote.GenerateDefaultSellerTitle(document, titleSignatory);

      BulkExchangeSolution.Module.Exchange.PublicFunctions.Module.SetDocumentResponsible(document, responsible);
      var caseFile = Sungero.BulkExchangeSolution.Module.Exchange.PublicFunctions.Module.GetDefaultCaseFile(document, false, isContractStatement);
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

    [Remote]
    public static void DisableJobs()
    {
      var jobs = Sungero.CoreEntities.Jobs.GetAll().Where(j => j.JobId == Constants.Module.VerifyJob ||
                                                          j.JobId == Constants.Module.SendSignedDocumentsJob).ToList();
      foreach (var job in jobs)
      {
        job.Status = Sungero.CoreEntities.DatabookEntry.Status.Closed;
        job.Save();
      }
    }
    
    [Remote]
    public static void StartVerifyDocuments()
    {
      ActivateJob(Constants.Module.VerifyJob);
      Sungero.BulkExchangeSolution.Module.Exchange.Jobs.VerifyDocuments.Enqueue();
    }

    [Remote]
    public static void StartGetMessages()
    {
      ActivateJob(Constants.Module.GetMessagesJob);
      Sungero.Exchange.Jobs.GetMessages.Enqueue();
    }
    
    [Remote]
    public static void SendSignedDocuments()
    {
      ActivateJob(Constants.Module.SendSignedDocumentsJob);
      Sungero.BulkExchangeSolution.Module.Exchange.Jobs.SendSignedDocuments.Enqueue();
    }
    
    public static void ActivateJob(Guid jobGuid)
    {
      var jobs = Sungero.CoreEntities.Jobs.GetAll().Where(j => j.JobId == jobGuid && j.Status == Sungero.CoreEntities.DatabookEntry.Status.Closed).ToList();
      foreach (var job in jobs)
      {
        job.Status = Sungero.CoreEntities.DatabookEntry.Status.Active;
        job.Save();
      }
    }
    
    #region Интеллектуальная обработка
    
    /// <summary>
    /// Обработать документ с сервиса обмена.
    /// </summary>
    /// <param name="document">Документ.</param>
    [Remote (IsPure = true), Public]
    public virtual void ProcessExchangeDocument(IOfficialDocument document)
    {
      var blobPackage = PrepareBlobPackage(document);
      
      ProcessPackageInArio(blobPackage);
      
      var arioPackage = SmartProcessing.PublicFunctions.Module.UnpackArioPackage(blobPackage);
      
      var documentPackage = this.BuildDocumentPackage(blobPackage, arioPackage);
      
      //this.OrderAndLinkDocumentPackage(documentPackage);
      
      //this.SendToResponsible(documentPackage);

      SmartProcessing.PublicFunctions.Module.FinalizeProcessing(blobPackage);
    }
    
    public virtual IBlobPackage PrepareBlobPackage(IOfficialDocument document)
    {
      var blobPackage = BlobPackages.Create();
      blobPackage.SenderLine = "ExchangeCaptureLine";
      
      var blob = Blobs.Create();
      blob.Document = document;
      blob.Save();

      blobPackage.Blobs.AddNew().Blob = blob;
      
      blobPackage.Save();
      
      return blobPackage;
    }

    /// <summary>
    /// Обработать документ из сервиса обмена в Ario.
    /// </summary>
    /// <param name="blobPackage">Пакет документов.</param>
    [Public]
    public virtual void ProcessPackageInArio(IBlobPackage blobPackage)
    {
      // Проверка настроек.
      var smartProcessingSettings = Sungero.Docflow.PublicFunctions.SmartProcessingSetting.GetSettings();
      if (smartProcessingSettings == null)
        throw new ApplicationException(Sungero.SmartProcessing.Resources.SmartProcessingSettingsNotFound);
      
      Sungero.Docflow.PublicFunctions.SmartProcessingSetting.ValidateSettings(smartProcessingSettings);
      var firstPageClassifierId = smartProcessingSettings.FirstPageClassifierId.ToString();
      var typeClassifierId = smartProcessingSettings.TypeClassifierId.ToString();
      
      // Получение доп. классификаторов.
      var additionalClassifierIds = Sungero.Docflow.PublicFunctions.SmartProcessingSetting.GetAdditionalClassifierIds(smartProcessingSettings);
      
      // Обработка в Ario.
      var arioConnector = new ArioExtensions.ArioConnector(smartProcessingSettings.ArioUrl);
      
      // Получить соответствие класса и наименования правила извлечения фактов.
      var processingRule = smartProcessingSettings.ProcessingRules
        .Where(x => !string.IsNullOrWhiteSpace(x.ClassName) && !string.IsNullOrWhiteSpace(x.GrammarName))
        .ToDictionary(x => x.ClassName, x => x.GrammarName);
      var blobs = blobPackage.Blobs.Select(x => x.Blob);
      foreach (IBlob blob in blobs)
      {
        var document = blob.Document;
        //if (!this.CanArioProcessFile(filePath))
        //{
        //  continue;
        //}
        
        try
        {
          byte[] content;
          using(var memory = new System.IO.MemoryStream())
          {
            using (var sourceStream = document.LastVersion.Body.Read())
              sourceStream.CopyTo(memory);
            content = memory.ToArray();
          }
          
          var arioResultJson = arioConnector.ClassifyAndExtractFacts(content,
                                                                     document.Name,
                                                                     typeClassifierId,
                                                                     firstPageClassifierId,
                                                                     processingRule,
                                                                     additionalClassifierIds);
          blob.ArioResultJson = arioResultJson;
          blob.Save();
        }
        catch (ExternalException ex)
        {
          throw ex;
        }
      }
    }
    
    /// <summary>
    /// Определить, может ли Ario обработать файл.
    /// </summary>
    /// <param name="fileName">Имя или путь до файла.</param>
    /// <returns>True - может, False - иначе.</returns>
    public virtual bool CanArioProcessFile(string fileName)
    {
      var ext = Path.GetExtension(fileName).TrimStart('.').ToLower();
      var allowedExtensions = new List<string>()
      {
        "jpg", "jpeg", "png", "bmp", "gif",
        "tif", "tiff", "pdf", "doc", "docx",
        "dot", "dotx", "rtf", "odt", "ott",
        "txt", "xls", "xlsx", "ods"
      };
      return allowedExtensions.Contains(ext);
    }
    
    /// <summary>
    /// Сформировать пакет документов.
    /// </summary>
    /// <param name="blobPackage">Пакет документов из DCS.</param>
    /// <param name="arioPackage">Пакет результатов обработки документов в Ario.</param>
    /// <returns>Пакет созданных документов.</returns>
    [Public]
    public virtual IDocumentPackage BuildDocumentPackage(IBlobPackage blobPackage, IArioPackage arioPackage)
    {
      var documentPackage = SmartProcessing.PublicFunctions.Module.PrepareDocumentPackage(blobPackage, arioPackage);
      
      foreach (var documentInfo in documentPackage.DocumentInfos)
      {
        var document = SmartProcessing.PublicFunctions.Module.CreateDocument(documentInfo, documentPackage);
        
        //this.CreateVersion(document, documentInfo);
        
        if (!documentInfo.FailedCreateVersionByBarcode)
        {
          //SmartProcessing.PublicFunctions.Module.FillDeliveryMethod(document, blobPackage.SourceType);
          
          SmartProcessing.PublicFunctions.Module.FillVerificationState(document);
        }
        
        SmartProcessing.PublicFunctions.Module.SaveDocument(document, documentInfo);
      }
      
      //this.CreateDocumentFromEmailBody(documentPackage);
      
      return documentPackage;
    }
    
    /// <summary>
    /// Создать акт выполненных работ.
    /// </summary>
    /// <param name="documentInfo">Информация о документе.</param>
    /// <param name="responsible">Ответственный за верификацию.</param>
    /// <returns>Акт выполненных работ.</returns>
    [Public]
    public virtual IOfficialDocument CreateContractStatementArio(IDocumentInfo documentInfo,
                                                             IEmployee responsible)
    {
      //System.Diagnostics.Debugger.Launch();
      // Акт выполненных работ.
      //var document = FinancialArchive.ContractStatements.Create();
      var document = BulkExchangeSolution.Blobs.As(documentInfo.ArioDocument.OriginalBlob).Document;
      //var contractStatement = document.ConvertTo(FinancialArchive.ContractStatements.Info);
      var contractStatement = FinancialArchive.ContractStatements.As(document.ConvertTo(FinancialArchive.ContractStatements.Info));
      //contractStatement.Save();
      
      SmartProcessing.PublicFunctions.Module.FillContractStatementProperties(contractStatement, documentInfo, responsible);
      
      return contractStatement;
    }
    
    #endregion

  }
}