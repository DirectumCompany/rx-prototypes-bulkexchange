using System;
using Sungero.Core;

namespace Sungero.BulkExchangeSolution.Constants
{
  public static class Module
  {
    public static readonly Guid GetMessagesJob = Guid.Parse("13f61e21-8bb9-4a3d-b72b-e92da64c60b4");
    public static readonly Guid VerifyJob = Guid.Parse("5db5338d-6db7-4463-8819-81587d164a5c");
    public static readonly Guid SendSignedDocumentsJob = Guid.Parse("317b0658-e0bb-4733-ac3c-df0b52ae95bf");
  }
}