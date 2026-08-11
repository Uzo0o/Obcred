using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Obcred.Models;

namespace Obcred.Services;

public interface IUjpService
{
    Task<CompanyDto> GetCompanyDetailsAsync(string edb);
    Task<string> GetServerTimestampAsync();
    Task<UjpSubmissionResult> SubmitInvoiceAsync(object invoicePayload);
    Task<List<PurchaseInvoiceStatusDto>> GetPurchaseInvoicesStatusAsync(DateTime dateFrom, DateTime dateTo);
}