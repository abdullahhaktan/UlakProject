using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ulak.Shared.Admin;
using Ulak.Shared.Auth;
using Ulak.Shared.Deliveries;

namespace Ulak.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class TenantFlowTests
{
    private const string DemoAdmin = "+905550001122";
    private const string OtherAdmin = "+905550002233";
    private const string AdminPassword = "Ops12345!";

    private readonly ApiFactory _factory;

    public TenantFlowTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Sign_up_creates_an_isolated_company_with_an_empty_delivery_list()
    {
        var client = _factory.CreateClient();
        var phone = $"+90555{Random.Shared.Next(1_000_000, 9_999_999)}";

        var signup = await client.PostAsJsonAsync("/signup",
            new SignUpRequest("Test Nakliyat", "Ali Veli", phone, "Test1234!"));
        signup.StatusCode.ShouldBe(HttpStatusCode.OK);

        var auth = await signup.Content.ReadFromJsonAsync<AuthResponse>();
        auth!.User.Role.ShouldBe("Admin");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var deliveries = await client.GetFromJsonAsync<PagedResponse<AdminDeliveryRow>>("/admin/deliveries");
        deliveries!.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Sign_up_rejects_a_phone_that_is_already_registered()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/signup",
            new SignUpRequest("Dup Co", "Dup Admin", DemoAdmin, "Test1234!"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Admin_creates_a_driver_who_can_then_log_in_and_must_change_password()
    {
        var admin = await AuthedClientAsync(DemoAdmin, AdminPassword);
        var phone = $"+90555{Random.Shared.Next(1_000_000, 9_999_999)}";

        var created = await admin.PostAsJsonAsync("/admin/drivers", new CreateDriverRequest("Yeni Surucu", phone));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await created.Content.ReadFromJsonAsync<CreateDriverResponse>();
        body!.TempPassword.ShouldNotBeNullOrWhiteSpace();

        var login = await _factory.CreateClient()
            .PostAsJsonAsync("/auth/login", new LoginRequest(phone, body.TempPassword));
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        auth!.User.Role.ShouldBe("Driver");
        auth.User.MustChangePassword.ShouldBeTrue();
    }

    [Fact]
    public async Task Creating_a_driver_sends_an_invite_sms_with_the_temp_password()
    {
        var admin = await AuthedClientAsync(DemoAdmin, AdminPassword);
        var phone = $"+90555{Random.Shared.Next(1_000_000, 9_999_999)}";

        var created = await admin.PostAsJsonAsync("/admin/drivers", new CreateDriverRequest("SMS Surucu", phone));
        var body = await created.Content.ReadFromJsonAsync<CreateDriverResponse>();

        var sms = _factory.Sms.Sent.Single(m => m.Phone == phone);
        sms.Body.ShouldContain(body!.TempPassword);
    }

    [Fact]
    public async Task An_admin_only_sees_their_own_companys_deliveries()
    {
        var demo = await AuthedClientAsync(DemoAdmin, AdminPassword);
        var other = await AuthedClientAsync(OtherAdmin, AdminPassword);

        var demoList = await demo.GetFromJsonAsync<PagedResponse<AdminDeliveryRow>>("/admin/deliveries?take=100");
        var otherList = await other.GetFromJsonAsync<PagedResponse<AdminDeliveryRow>>("/admin/deliveries?take=100");

        demoList!.Items.ShouldAllBe(d => d.OrderRef.StartsWith("ORD-"));
        otherList!.Items.ShouldAllBe(d => d.OrderRef.StartsWith("ON-"));
        demoList.Items.Select(d => d.OrderRef).ShouldNotContain("ON-5001");
    }

    [Fact]
    public async Task A_driver_from_another_company_gets_403_when_proving_a_foreign_delivery()
    {
        // Ornek Nakliyat's driver against a Ulak Demo delivery (id 2)
        var foreignDriver = await AuthedClientAsync("+905557778899", "Driver123!");

        var request = new
        {
            clientUuid = Guid.NewGuid(),
            deliveryId = 2,
            status = "Delivered",
            failureReason = (string?)null,
            recipientSignedName = (string?)null,
            signatureUrl = (string?)null,
            photoUrls = Array.Empty<string>(),
            capturedLat = (decimal?)null,
            capturedLng = (decimal?)null,
            capturedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        };

        var response = await foreignDriver.PostAsJsonAsync("/proofs", request);
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
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
