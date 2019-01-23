using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Midway.ServiceClient;
using Midway.ObjectModel;
using Midway.Crypto;
using System.Security.Cryptography.X509Certificates;

namespace SynerdocsExtensions
{
  public class Messages
  {
    public static void SendDocuments(string thumbprint, string receiverTin, string[] paths)
    {
      var url = "https://testservice.synerdocs.ru/exchangeservice.svc";
      var appId = new Guid().ToString();

      var client = new Client(url, false, false, "", "WSHttpsBinding_IExchangeService");
      var certificate = GetLocalCertificate(thumbprint);
      if (certificate == null)
        throw new Exception("Не найден сертификат по отпечатку.");

      if (!client.AuthenticateWithCertificate(certificate.Thumbprint, appId))
      {
        throw new Exception("Ошибка аутентификации в сервисе.");
      }

      var counterparty = client.GetOrganizationListByInnKpp(receiverTin, string.Empty).First();

      var documents = new List<Document>();
      var signs = new List<Sign>();

      foreach (var path in paths)
      {
        var fileBytes = File.ReadAllBytes(path);

        var document = CreateDocument(path, fileBytes);
        documents.Add(document);

        var signature = CryptoApiHelper.Sign(certificate, fileBytes, true);
        var sign = CreateSign(document, signature);
        signs.Add(sign);
      }

      var boxInfo = client.GetBoxes().FirstOrDefault();
      if (boxInfo == null)
        throw new Exception("Ошибка при получении ящика.");
      var currentBox = boxInfo.Address;

      var message = new Message
      {
        Id = Guid.NewGuid().ToString(),
        From = currentBox,
        Documents = documents.ToArray(),
        Recipients = new MessageRecipient[] { new MessageRecipient { OrganizationBoxId = counterparty.BoxAddress } },
        Signs = signs.ToArray()
      };
      SentMessage result;
      try
      {
        result = client.SendMessage(message);
      }
      catch (Exception ex)
      {
        throw new Exception("Ошибка при отправке документов: " + ex.Message);
      }
    }

    private static Document CreateDocument(string path, byte[] fileBytes)
    {
      var needSign = !path.Contains("Cчет-фактура №");
      var documentType = DocumentType.Untyped;
      if (path.Contains("Документ об отгрузке товаров") || 
        path.Contains("Cчет-фактура №") || 
        path.Contains("Счет-фактура и документ об отгрузке товаров") ||
        path.Contains("УПД"))
        documentType = DocumentType.GeneralTransferSeller;

      return new Document
      {
        Id = Guid.NewGuid().ToString(),
        DocumentType = documentType,
        FileName = Path.GetFileName(path),
        Content = fileBytes,
        Card = null,
        NeedSign = needSign
      };
    }

    private static Sign CreateSign(Document document, byte[] signature)
    {
      return new Sign
      {
        Id = Guid.NewGuid().ToString(),
        DocumentId = document.Id,
        Raw = signature
      };
    }

    private static X509Certificate2 GetLocalCertificate(string thumbprint)
    {
      Func<X509Certificate2, bool> certCondition =
          x => x.Thumbprint != null && x.Thumbprint.Equals(thumbprint, StringComparison.InvariantCultureIgnoreCase);

      var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
      store.Open(OpenFlags.ReadOnly);

      var validGostAlgorithms = new[]
      {
         "1.2.643.2.2.4",
         "1.2.643.2.2.3"
      };

      var validCertificates = store.Certificates.Cast<X509Certificate2>()
          .Where(x => validGostAlgorithms.Contains(x.SignatureAlgorithm.Value));

      var allowed = validCertificates.Where(certCondition).ToArray();
      if (allowed.Count() > 1)
        throw new Exception("Найдено более одного сертификата, удовлетворяющего" +
                                            " условию поиска");

      var cert = allowed.FirstOrDefault();
      if (cert == null)
        throw new Exception("Не найдено ни одного сертификата, удовлетворяющего" +
                                            " условию поиска и подходящего для работы в сервисе");

      if (!cert.HasPrivateKey)
        throw new Exception(string.Format(
            "Не найден закрытый ключ для сертификата с отпечатком {0}", cert.Thumbprint));

      return cert;
    }
  }
}

