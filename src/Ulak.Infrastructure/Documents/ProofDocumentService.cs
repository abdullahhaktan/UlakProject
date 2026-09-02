using ClosedXML.Excel;
using Ulak.Core.Abstractions;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Ulak.Infrastructure.Documents;

public sealed class ProofDocumentService : IProofDocumentService
{
    private readonly IProofRepository _proofs;
    private readonly IObjectStorage _storage;
    private readonly ILogger<ProofDocumentService> _logger;

    public ProofDocumentService(
        IProofRepository proofs, IObjectStorage storage, ILogger<ProofDocumentService> logger)
    {
        _proofs = proofs;
        _storage = storage;
        _logger = logger;
    }

    public async Task<byte[]?> RenderProofPdfAsync(int companyId, long proofId, CancellationToken ct)
    {
        var proof = await _proofs.GetByIdAsync(companyId, proofId, ct);
        if (proof is null)
        {
            return null;
        }

        var photos = new List<byte[]>();
        foreach (var photo in proof.Photos)
        {
            var bytes = await TryDownload(photo.Url, ct);
            if (bytes is not null)
            {
                photos.Add(bytes);
            }
        }

        var signature = string.IsNullOrEmpty(proof.SignatureUrl)
            ? null
            : await TryDownload(proof.SignatureUrl, ct);

        var document = Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(t => t.FontSize(10));

            page.Header().Column(header =>
            {
                header.Item().Text("Proof of Delivery / Teslimat Kanıtı").FontSize(16).SemiBold();
                header.Item().Text($"Order {proof.OrderRef}  •  Proof #{proof.Id}").FontColor(Colors.Grey.Darken1);
            });

            page.Content().PaddingVertical(12).Column(body =>
            {
                body.Spacing(10);

                body.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Recipient / Alıcı").SemiBold();
                        c.Item().Text(proof.RecipientName);
                        if (!string.IsNullOrEmpty(proof.RecipientPhone))
                        {
                            c.Item().Text(proof.RecipientPhone);
                        }

                        c.Item().Text(proof.AddressText);
                    });
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Delivery / Teslimat").SemiBold();
                        c.Item().Text($"Type: {proof.ProofType}");
                        c.Item().Text($"Status: {proof.Status}");
                        if (!string.IsNullOrEmpty(proof.FailureReason))
                        {
                            c.Item().Text($"Reason: {proof.FailureReason}");
                        }

                        c.Item().Text($"Driver: {proof.DriverName} ({proof.DriverPhone})");
                        c.Item().Text($"Captured (UTC): {proof.CapturedAtUtc:yyyy-MM-dd HH:mm}");
                        c.Item().Text($"Synced (UTC): {proof.SyncedAtUtc:yyyy-MM-dd HH:mm}");
                        if (proof.CapturedLat is { } lat && proof.CapturedLng is { } lng)
                        {
                            c.Item().Text($"GPS: {lat:F6}, {lng:F6}");
                            c.Item().Text($"maps.google.com/?q={lat:F6},{lng:F6}")
                                .FontColor(Colors.Blue.Medium).FontSize(8);
                        }
                    });
                });

                if (signature is not null)
                {
                    body.Item().Column(c =>
                    {
                        c.Item().Text("Signature / İmza").SemiBold();
                        c.Item().Width(200).Image(signature);
                    });
                }

                if (photos.Count > 0)
                {
                    body.Item().Text($"Photos / Fotoğraflar ({photos.Count})").SemiBold();
                    foreach (var pair in photos.Chunk(2))
                    {
                        body.Item().Row(row =>
                        {
                            row.Spacing(6);
                            foreach (var photo in pair)
                            {
                                row.RelativeItem().Height(180).Image(photo).FitArea();
                            }

                            if (pair.Length == 1)
                            {
                                row.RelativeItem();
                            }
                        });
                    }
                }
            });

            page.Footer().AlignCenter().Text(t =>
            {
                t.Span("Ulak • generated ");
                t.Span($"{DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
            });
        }));

        return document.GeneratePdf();
    }

    public async Task<byte[]> ExportProofsXlsxAsync(int companyId, ProofSearchQuery query, CancellationToken ct)
    {
        var exportQuery = query with { Skip = 0, Take = 5000 };
        var page = await _proofs.AdminSearchAsync(companyId, exportQuery, ct);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Proofs");

        string[] headers =
        [
            "Proof Id", "Order Ref", "Recipient", "Address", "Type", "Status", "Failure Reason",
            "Driver", "Photos", "Captured (UTC)", "Synced (UTC)",
        ];
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
        }

        sheet.Row(1).Style.Font.Bold = true;

        var r = 2;
        foreach (var row in page.Items)
        {
            sheet.Cell(r, 1).Value = row.Id;
            sheet.Cell(r, 2).Value = row.OrderRef;
            sheet.Cell(r, 3).Value = row.RecipientName;
            sheet.Cell(r, 4).Value = row.AddressText;
            sheet.Cell(r, 5).Value = row.ProofType;
            sheet.Cell(r, 6).Value = row.Status;
            sheet.Cell(r, 7).Value = row.FailureReason;
            sheet.Cell(r, 8).Value = row.DriverName;
            sheet.Cell(r, 9).Value = row.PhotoCount;
            sheet.Cell(r, 10).Value = row.CapturedAtUtc;
            sheet.Cell(r, 11).Value = row.SyncedAtUtc;
            r++;
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task<byte[]?> TryDownload(string key, CancellationToken ct)
    {
        byte[] bytes;
        try
        {
            bytes = await _storage.ReadAllBytesAsync(key, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not download object {Key} for the proof PDF", key);
            return null;
        }

        try
        {
            _ = QuestPDF.Infrastructure.Image.FromBinaryData(bytes);
            return bytes;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Object {Key} is not a decodable image; skipping it in the proof PDF", key);
            return null;
        }
    }
}
