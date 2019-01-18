using System;
using Sungero.Core;

namespace Sungero.BulkExchangeSolution.Constants.Exchange
{
  public static class ExchangeDocumentInfo
  {
    public static class DocumentSetType
    {
      // Реализация товаров.
      public const string Waybill = "Waybill";
      
      // Оказание услуг (выполнение работ).
      public const string ContractStatement = "ContractStatement";
    }
  }
}