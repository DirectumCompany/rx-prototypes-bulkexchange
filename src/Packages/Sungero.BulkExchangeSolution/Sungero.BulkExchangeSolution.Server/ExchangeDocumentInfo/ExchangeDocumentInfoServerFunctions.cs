using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.BulkExchangeSolution.ExchangeDocumentInfo;
using Sungero.BulkExchangeSolution.Module.Exchange.Server;
using Sungero.Core;
using Sungero.CoreEntities;

namespace Sungero.BulkExchangeSolution.Server
{
  partial class ExchangeDocumentInfoFunctions
  {
    /// <summary>
    /// Получить все комплекты документов по указанным инфошкам.
    /// </summary>
    /// <param name="infos">Инфошки документов.</param>
    /// <returns>Комплекты.</returns>
    public static List<Structures.Exchange.ExchangeDocumentInfo.DocumentSet> GetDocumentSets(List<IExchangeDocumentInfo> infos)
    {
      var documentSets = new List<Structures.Exchange.ExchangeDocumentInfo.DocumentSet>();
      foreach (var info in infos)
      {
        var newSet = Functions.ExchangeDocumentInfo.GetDocumentSet(info);
        // Две разные инфошки могут дать на выходе один и тот же комплект, проверяем на дубли перед добавлением.
        if (newSet != null && !documentSets.Contains(newSet))
          documentSets.Add(newSet);
      }
      return documentSets;
    }
    
    /// <summary>
    /// Получить комплект документов.
    /// </summary>
    /// <returns>Структура с комплектом - признак полноты и инфошки. Может быть null, если в сообщении нет никаких признаков комплекта.</returns>
    public virtual Structures.Exchange.ExchangeDocumentInfo.DocumentSet GetDocumentSet()
    {
      // Обработка по номеру заказа. Если номера заказа нет - вернётся null.
      var documentSet = this.GetPurchaseOrderDocumentSet();
      
      // TODO в теории тут будет обработка других типов комплектов, не только товарных накладных.
      return documentSet;
    }
    
    protected virtual Structures.Exchange.ExchangeDocumentInfo.DocumentSet GetPurchaseOrderDocumentSet()
    {
      // Берем все инфошки по сообщению - нам надо их проанализировать (сортируем, чтобы создавались гарантированно одинаковые структуры).
      var infos = Sungero.BulkExchangeSolution.ExchangeDocumentInfos
        .GetAll(i => Equals(i.RootBox, _obj.RootBox) && i.ServiceMessageId == _obj.ServiceMessageId)
        .OrderBy(i => i.Id)
        .ToList();

      // Берем уникальный признак решения - номер заказа.
      var uniquePurchaseOrders = infos.Select(i => i.PurchaseOrder).Distinct().ToList();
      
      // Если номера заказа в сообщении нет - комплекта тоже нет.
      if (uniquePurchaseOrders.All(po => string.IsNullOrWhiteSpace(po)))
        return null;
      
      // Если в сообщении упомянуты разные номера заказов - комплект "некорректный".
      if (uniquePurchaseOrders.Count > 1)
        return Structures.Exchange.ExchangeDocumentInfo.DocumentSet.Create(false, infos);
      
      if (infos.Count == 1)
      {
        // Если в сообщении только один документ и он с функцией СЧФДОП - это "корректный" комплект.
        var full = Functions.ExchangeDocumentInfo.ExchangeDocumentInfoHasFunction(infos.Single(), Docflow.AccountingDocumentBase.FormalizedFunction.SchfDop);
        return Structures.Exchange.ExchangeDocumentInfo.DocumentSet.Create(full, infos);
      }
      else if (infos.Count == 2)
      {
        // Если в сообщении только два документа и это СЧФ и ДОП - это "корректный" комплект.
        var hasSchf = infos.Any(i => Functions.ExchangeDocumentInfo.ExchangeDocumentInfoHasFunction(i, Docflow.AccountingDocumentBase.FormalizedFunction.Schf));
        var hasDop = infos.Any(i => Functions.ExchangeDocumentInfo.ExchangeDocumentInfoHasFunction(i, Docflow.AccountingDocumentBase.FormalizedFunction.Dop));
        return Structures.Exchange.ExchangeDocumentInfo.DocumentSet.Create(hasSchf && hasDop, infos);
      }
      
      // Во всех остальных случаях, когда например документов в сообщении больше - комплект считаем "некорректным".
      return Structures.Exchange.ExchangeDocumentInfo.DocumentSet.Create(false, infos);
    }
    
    /// <summary>
    /// Проверка инфошки на требования комплекта -- функции.
    /// </summary>
    /// <param name="function">Функция (СЧФ, ДОП, СЧФДОП).</param>
    /// <returns>True, если документ по инфошке с указанной функцией.</returns>
    public virtual bool ExchangeDocumentInfoHasFunction(Sungero.Core.Enumeration function)
    {
      var document = Docflow.AccountingDocumentBases.As(_obj.Document);
      
      if (document == null)
        return false;
      
      return document.FormalizedFunction == function;
    }
    
    /// <summary>
    /// Отпралять задания/уведомления ответственному.
    /// </summary>
    /// <returns>Признак отправки задания ответсвенному за ящик.</returns>
    [Public]
    public override bool NeedReceiveTask()
    {
      return _obj.PurchaseOrder != null;
    }
  }
}