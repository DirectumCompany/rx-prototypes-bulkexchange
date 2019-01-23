using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SynerdocsExample
{
  class Program
  {
    static void Main(string[] args)
    {
      var tin = "1835128323";
      var path1 = @"..\..\..\..\..\SynerdocsGenerator\samples\Акт (с № договора, УПДдоп + СФ)\Документ об отгрузке товаров (выполнении работ), передаче имущественных прав (Документ об оказании услуг) № 238 от 21.01.xml";
      var path2 = @"..\..\..\..\..\SynerdocsGenerator\samples\Акт (с № договора, УПДдоп + СФ)\Cчет-фактура № 235 от 18.01.19.xml";
      var thumbprint = "‎30b8258e8f217650bb82c0fd33d259d5214f35ce";

      SynerdocsExtensions.Messages.SendDocuments(thumbprint, tin, new string[] { path1, path2 });
      Console.WriteLine("Документы успешно отправлены.");
      Console.ReadKey();
    }
  }
}
