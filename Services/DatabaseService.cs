using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Obcred.Models;
using SQLite;

namespace Obcred.Services;

public class DatabaseService : IDatabaseService
{
    private readonly SQLiteAsyncConnection _db;

    public DatabaseService()
    {
        // Save the database in the exact same IntegritiEFakturi AppData folder
        string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string myAppFolder = Path.Combine(appDataFolder, "IntegritiEFakturi");
        Directory.CreateDirectory(myAppFolder);
        
        string dbPath = Path.Combine(myAppFolder, "efakturi-data.db");
        
        // Initialize connection
        _db = new SQLiteAsyncConnection(dbPath);
        
        // This automatically creates the tables if they don't exist yet!
        _db.CreateTableAsync<ClientRecord>().Wait();
        _db.CreateTableAsync<InvoiceRecord>().Wait();
        _db.CreateTableAsync<InvoiceSequence>().Wait();
    }

    public async Task SaveClientAsync(ClientRecord client)
    {
        // InsertOrReplace ensures if the EDB already exists, it updates the address instead of crashing
        await _db.InsertOrReplaceAsync(client);
    }

    public async Task<List<ClientRecord>> SearchClientsByNameAsync(string searchQuery)
    {
        // Perform a case-insensitive SQL LIKE search
        return await _db.Table<ClientRecord>()
            .Where(c => c.Name.ToLower().Contains(searchQuery.ToLower()))
            .ToListAsync();
    }

    public async Task<ClientRecord> GetClientByEdbAsync(string edb)
    {
        return await _db.Table<ClientRecord>()
            .FirstOrDefaultAsync(c => c.Edb == edb);
    }

    public async Task<List<ClientRecord>> GetAllClientsAsync()
    {
        return await _db.Table<ClientRecord>().OrderBy(c => c.Name).ToListAsync();
    }

    public async Task SaveInvoiceAsync(InvoiceRecord invoice)
    {
        // Insert (never replace): every submission attempt is its own immutable record.
        await _db.InsertAsync(invoice);
    }

    public async Task<List<InvoiceRecord>> GetAllInvoicesAsync()
    {
        return await _db.Table<InvoiceRecord>()
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task<InvoiceRecord> GetInvoiceByIdAsync(int id)
    {
        return await _db.Table<InvoiceRecord>().FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<int> PeekNextInvoiceSeqAsync(int year)
    {
        // The next number that WOULD be assigned this year, without advancing it.
        var row = await _db.FindAsync<InvoiceSequence>(year);
        return row?.NextValue ?? 1;
    }

    public async Task CommitInvoiceSeqAsync(int year)
    {
        // Advance the counter. Called only after a successful UJP submission.
        var row = await _db.FindAsync<InvoiceSequence>(year);
        int current = row?.NextValue ?? 1;
        await _db.InsertOrReplaceAsync(new InvoiceSequence { Year = year, NextValue = current + 1 });
    }
}