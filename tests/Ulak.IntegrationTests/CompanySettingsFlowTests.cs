using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ulak.Shared.Admin;
using Ulak.Shared.Auth;
using Ulak.Shared.Deliveries;
using Ulak.Shared.Proofs;

namespace Ulak.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class CompanySettingsFlowTests
{
    private readonly ApiFactory _factory;

    public CompanySettingsFlowTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task An_admin_can_read_and_update_the_company_settings()
    {
        var admin = await NewCompanyAdminAsync();

        var initial = await admin.GetFromJsonAsync<CompanyConfigDto>("/admin/settings");
        initial!.RequirePhoto.ShouldBeTrue();        // column default
        initial.RequireSignature.ShouldBeFalse();

        var updated = await admin.PutAsJsonAsync("/admin/settings",
            new UpdateCompanySettingsRequest("Yeni Ad", RequirePhoto: false, RequireSignature: true));
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);

        var saved = await admin.GetFromJsonAsync<CompanyConfigDto>("/admin/settings");
        saved!.DisplayName.ShouldBe("Yeni Ad");
        saved.RequirePhoto.ShouldBeFalse();
        saved.RequireSignature.ShouldBeTrue();
    }

    [Fact]
    public async Task RequirePhoto_blocks_a_photoless_delivery_proof_until_it_is_turned_off()
    {
        var admin = await NewCompanyAdminAsync();

        var driverPhone = $"+90555{Random.Shared.Next(1_000_000, 9_999_999)}";
        var driverResponse = await admin.PostAsJsonAsync("/admin/drivers", new CreateDriverRequest("Surucu", driverPhone));
        var driverBody = (await driverResponse.Content.ReadFromJsonAsync<CreateDriverResponse>())!;
        var driverId = driverBody.Id;
        var driverPw = driverBody.TempPassword;

        var delivery = await admin.PostAsJsonAsync("/deliveries", new CreateDeliveryRequest(
            OrderRef: $"S-{Random.Shared.Next(100_000, 999_999)}",
            RecipientName: "Alici", RecipientPhone: null, AddressText: "Adres",
            Lat: null, Lng: null, Note: null, AssignedDriverId: driverId));
        var deliveryId = (await delivery.Content.ReadFromJsonAsync<DeliveryDetail>())!.Id;

        var driverClient = await AuthedClientAsync(driverPhone, driverPw);

        // pickup (with a photo) -> PickedUp
        var pickup = await driverClient.PostAsJsonAsync("/proofs", new CreateProofRequest(
            Guid.NewGuid(), deliveryId, "PickedUp", null, "Depo", null,
            ["photos/it/pickup.jpg"], null, null, DateTimeOffset.UtcNow.AddMinutes(-2), "Pickup"));
        pickup.EnsureSuccessStatusCode();

        var photoless = new CreateProofRequest(
            Guid.NewGuid(), deliveryId, "Delivered", null, "Alici", "signatures/it/s.png",
            PhotoUrls: [], null, null, DateTimeOffset.UtcNow.AddMinutes(-1));

        var blocked = await driverClient.PostAsJsonAsync("/proofs", photoless);
        blocked.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        // admin turns the requirement off
        (await admin.PutAsJsonAsync("/admin/settings",
            new UpdateCompanySettingsRequest("Firma", RequirePhoto: false, RequireSignature: false)))
            .EnsureSuccessStatusCode();

        var allowed = await driverClient.PostAsJsonAsync("/proofs", photoless with { ClientUuid = Guid.NewGuid() });
        allowed.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task<HttpClient> NewCompanyAdminAsync()
    {
        var client = _factory.CreateClient();
        var phone = $"+90555{Random.Shared.Next(1_000_000, 9_999_999)}";
        var signup = await client.PostAsJsonAsync("/signup",
            new SignUpRequest("Ayar Test", "Admin", phone, "Test1234!"));
        signup.EnsureSuccessStatusCode();
        var auth = await signup.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private async Task<HttpClient> AuthedClientAsync(string phone, string password)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest(phone, password));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }
}
