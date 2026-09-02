using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ulak.Shared.Auth;
using Ulak.Shared.Proofs;

namespace Ulak.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class ProofFlowTests
{
    private const string Driver1 = "+905551112233";
    private const string Driver2 = "+905554445566";
    private const string Ops = "+905550001122";
    private const string Password = "Driver123!";
    private const string OpsPassword = "Ops12345!";

    private readonly ApiFactory _factory;

    public ProofFlowTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_returns_a_token_for_a_seeded_user()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest(Driver1, Password));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth!.AccessToken.ShouldNotBeNullOrWhiteSpace();
        auth.User.Role.ShouldBe("Driver");
    }

    [Fact]
    public async Task Login_with_a_wrong_password_is_401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest(Driver1, "nope"));
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_driver_can_read_a_teammates_delivery_but_not_a_delivery_from_another_company()
    {
        var client = await AuthedClientAsync(Driver1, Password);

        // ORD-24004 (id 4) is assigned to driver 2 in the SAME company -> readable
        var teammate = await client.GetAsync("/deliveries/4");
        teammate.StatusCode.ShouldBe(HttpStatusCode.OK);

        // ids 11+ belong to "Ornek Nakliyat" -> not visible
        var otherCompany = await client.GetAsync("/deliveries/11");
        otherCompany.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Submitting_the_same_proof_twice_creates_exactly_one_record()
    {
        var driver = await AuthedClientAsync(Driver1, Password);

        // pickup first — a delivery proof is rejected until the parcel is picked up
        (await driver.PostAsJsonAsync("/proofs", PickupFor(3))).EnsureSuccessStatusCode();

        var clientUuid = Guid.NewGuid();
        var request = new CreateProofRequest(
            clientUuid,
            DeliveryId: 3,                 // ORD-24003, assigned to driver 1
            Status: "Delivered",
            FailureReason: null,
            RecipientSignedName: "Test Alici",
            SignatureUrl: "signatures/it/sig.png",
            PhotoUrls: ["photos/it/a.jpg", "photos/it/b.jpg"],
            CapturedLat: 41.0m,
            CapturedLng: 29.0m,
            CapturedAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        var first = await driver.PostAsJsonAsync("/proofs", request);
        var second = await driver.PostAsJsonAsync("/proofs", request);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);

        var firstBody = await first.Content.ReadFromJsonAsync<CreateProofResponse>();
        var secondBody = await second.Content.ReadFromJsonAsync<CreateProofResponse>();
        firstBody!.WasDuplicate.ShouldBeFalse();
        secondBody!.WasDuplicate.ShouldBeTrue();
        secondBody.Id.ShouldBe(firstBody.Id);

        // the ops panel sees the pickup + the (single) delivery proof for that order
        var ops = await AuthedClientAsync(Ops, OpsPassword);
        var page = await ops.GetFromJsonAsync<PagedProofs>("/admin/proofs?search=ORD-24003");
        page!.TotalCount.ShouldBe(2);
        var delivery = page.Items.Single(p => p.ProofType == "Delivery");
        delivery.PhotoCount.ShouldBe(2);
    }

    [Fact]
    public async Task A_delivery_proof_is_rejected_until_the_pickup_proof_exists()
    {
        var driver = await AuthedClientAsync(Driver1, Password);

        var delivery = new CreateProofRequest(
            Guid.NewGuid(), DeliveryId: 2, Status: "Delivered", FailureReason: null,
            RecipientSignedName: null, SignatureUrl: null, PhotoUrls: ["photos/it/x.jpg"],
            CapturedLat: null, CapturedLng: null, CapturedAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        var tooEarly = await driver.PostAsJsonAsync("/proofs", delivery);
        tooEarly.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await driver.PostAsJsonAsync("/proofs", PickupFor(2))).EnsureSuccessStatusCode();

        var now = await driver.PostAsJsonAsync("/proofs", delivery);
        now.StatusCode.ShouldBe(HttpStatusCode.Created);

        // delivery is Delivered, and both proofs are on record
        var detail = await driver.GetFromJsonAsync<DeliveryDetailDto>("/deliveries/2");
        detail!.Status.ShouldBe("Delivered");
    }

    [Fact]
    public async Task A_failed_pickup_sends_the_delivery_straight_to_failed()
    {
        var driver = await AuthedClientAsync(Driver1, Password);

        var failedPickup = new CreateProofRequest(
            Guid.NewGuid(), DeliveryId: 9, Status: "Failed",
            FailureReason: "Satici paketi teslim etmedi",
            RecipientSignedName: null, SignatureUrl: null, PhotoUrls: [],
            CapturedLat: null, CapturedLng: null, CapturedAt: DateTimeOffset.UtcNow.AddMinutes(-1),
            ProofType: "Pickup");

        (await driver.PostAsJsonAsync("/proofs", failedPickup)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var detail = await driver.GetFromJsonAsync<DeliveryDetailDto>("/deliveries/9");
        detail!.Status.ShouldBe("Failed");
    }

    private static CreateProofRequest PickupFor(int deliveryId) => new(
        Guid.NewGuid(), deliveryId, Status: "PickedUp", FailureReason: null,
        RecipientSignedName: "Depo", SignatureUrl: null, PhotoUrls: ["photos/it/pickup.jpg"],
        CapturedLat: null, CapturedLng: null, CapturedAt: DateTimeOffset.UtcNow.AddMinutes(-2),
        ProofType: "Pickup");

    private sealed record DeliveryDetailDto(int Id, string OrderRef, string Status);

    [Fact]
    public async Task A_driver_cannot_submit_a_proof_for_an_unassigned_delivery()
    {
        var driver = await AuthedClientAsync(Driver1, Password);

        var request = new CreateProofRequest(
            Guid.NewGuid(),
            DeliveryId: 5,                 // ORD-24005, assigned to driver 2
            Status: "Delivered",
            FailureReason: null,
            RecipientSignedName: null,
            SignatureUrl: null,
            PhotoUrls: [],
            CapturedLat: null,
            CapturedLng: null,
            CapturedAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        var response = await driver.PostAsJsonAsync("/proofs", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
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

    private sealed record PagedProofs(IReadOnlyList<ProofListItem> Items, int TotalCount);
}
