using System;
using Sungero.Core;

namespace Sungero.BulkExchangeSolution.Module.Exchange.Constants
{
  public static class Module
  {
    public const int DocumentMaxTotalAmount = 100000;
    public const int DocumentVerificationDeadlineInHours = 1;
    public const string RepeatRegister = "repeatRegister";
    public const string PurchaseOrder = "номер_заказа";
    public const string ContractNumber = "номер_договора";
    
    // GUID роли "Главный бухгалтер".
    public const string ChiefAccountantRoleName = "Главный бухгалтер";
  }
}