using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Obcred.Models;
using SQLite;

namespace Obcred.Services;

public class DatabaseService : IDatabaseService
{
    private readonly string _appDataFolder;
    private SQLiteAsyncConnection? _db;

    public DatabaseService()
    {
        // Save the database in the exact same IntegritiEFakturi AppData folder
        string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _appDataFolder = Path.Combine(appDataFolder, "IntegritiEFakturi");
        Directory.CreateDirectory(_appDataFolder);

        // No connection is opened here — we don't know which account is
        // signed in yet. SwitchUserAsync opens the right one once we do.
    }

    /// <summary>
    /// Points this service at the signed-in account's own SQLite file, so
    /// invoices/clients never leak between accounts sharing this machine. Every
    /// account — including the very first to log in after this machine had a
    /// single shared database — starts with a blank database of its own; the
    /// pre-multi-account file is left untouched on disk (never deleted, never
    /// copied) purely as a manual-recovery backup. Safe to call again on a
    /// logout -> different account login within the same run.
    /// </summary>
    public async Task SwitchUserAsync(string userId)
    {
        if (_db is not null)
            await _db.CloseAsync();

        string userFolder = Path.Combine(_appDataFolder, "Users", UserStorageKey.From(userId));
        Directory.CreateDirectory(userFolder);

        string dbPath = Path.Combine(userFolder, "efakturi-data.db");
        var db = new SQLiteAsyncConnection(dbPath);

        // This automatically creates the tables if they don't exist yet!
        await db.CreateTableAsync<ClientRecord>();
        await db.CreateTableAsync<InvoiceRecord>();
        await db.CreateTableAsync<InvoiceSequence>();
        await db.CreateTableAsync<PurchaseInvoiceRecord>();

        _db = db;
    }

    private SQLiteAsyncConnection Db => _db ??
        throw new InvalidOperationException("DatabaseService used before SwitchUserAsync — no account is signed in yet.");

    public async Task SaveClientAsync(ClientRecord client)
    {
        // InsertOrReplace ensures if the EDB already exists, it updates the address instead of crashing
        await Db.InsertOrReplaceAsync(client);
    }

    public async Task<List<ClientRecord>> SearchClientsByNameAsync(string searchQuery)
    {
        // Perform a case-insensitive SQL LIKE search
        return await Db.Table<ClientRecord>()
            .Where(c => c.Name.ToLower().Contains(searchQuery.ToLower()))
            .ToListAsync();
    }

    public async Task<ClientRecord> GetClientByEdbAsync(string edb)
    {
        return await Db.Table<ClientRecord>()
            .FirstOrDefaultAsync(c => c.Edb == edb);
    }

    public async Task<List<ClientRecord>> GetAllClientsAsync()
    {
        return await Db.Table<ClientRecord>().OrderBy(c => c.Name).ToListAsync();
    }

    public async Task SaveInvoiceAsync(InvoiceRecord invoice)
    {
        // Insert (never replace): every submission attempt is its own immutable record.
        await Db.InsertAsync(invoice);
    }

    public async Task<List<InvoiceRecord>> GetAllInvoicesAsync()
    {
        return await Db.Table<InvoiceRecord>()
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<InvoiceRecord> GetInvoiceByIdAsync(int id)
    {
        return await Db.Table<InvoiceRecord>().FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task SavePurchaseInvoiceAsync(PurchaseInvoiceRecord invoice)
    {
        // Keyed by Euid: InsertOrReplace ensures a re-sync from UJP updates the cached
        // status/amounts without needing to touch the locally-tracked IsPaid flag —
        // the caller is responsible for carrying that value over before calling this.
        await Db.InsertOrReplaceAsync(invoice);
    }

    public async Task<List<PurchaseInvoiceRecord>> GetAllPurchaseInvoicesAsync()
    {
        return await Db.Table<PurchaseInvoiceRecord>()
            .OrderByDescending(i => i.DocDate)
            .ToListAsync();
    }

    public async Task<PurchaseInvoiceRecord?> GetPurchaseInvoiceByEuidAsync(string euid)
    {
        return await Db.Table<PurchaseInvoiceRecord>().FirstOrDefaultAsync(i => i.Euid == euid);
    }

    public async Task SetPurchaseInvoicePaidAsync(string euid, bool isPaid)
    {
        var record = await GetPurchaseInvoiceByEuidAsync(euid);
        if (record == null) return;

        record.IsPaid = isPaid;
        await Db.UpdateAsync(record);
    }

    public async Task<int> PeekNextInvoiceSeqAsync(int year)
    {
        // The next number that WOULD be assigned this year, without advancing it.
        var row = await Db.FindAsync<InvoiceSequence>(year);
        return row?.NextValue ?? 1;
    }

    public async Task CommitInvoiceSeqAsync(int year)
    {
        // Advance the counter. Called only after a successful UJP submission.
        var row = await Db.FindAsync<InvoiceSequence>(year);
        int current = row?.NextValue ?? 1;
        await Db.InsertOrReplaceAsync(new InvoiceSequence { Year = year, NextValue = current + 1 });
    }
}
