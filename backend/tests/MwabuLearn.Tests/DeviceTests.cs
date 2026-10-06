using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MwabuLearn.Application.Devices;
using MwabuLearn.Application.Identity;
using MwabuLearn.Domain.Entities.Devices;
using MwabuLearn.Infrastructure.Persistence;
namespace MwabuLearn.Tests;

public sealed class DeviceTests
{
    private static RegisterDeviceRequest Registration() => new(Guid.NewGuid(), "Shared classroom tablet", DevicePlatform.Android, "2.0.0");
    private static void Proof(HttpClient client, DeviceRegistrationResponse registration)
    {
        client.DefaultRequestHeaders.Remove("X-Device-Id"); client.DefaultRequestHeaders.Remove("X-Device-Credential");
        client.DefaultRequestHeaders.Add("X-Device-Id", registration.Device.Id.ToString());
        client.DefaultRequestHeaders.Add("X-Device-Credential", registration.Credential);
    }
    [Fact]
    public async Task Registration_is_authenticated_idempotent_and_never_replays_credentials()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize();
        var teacher = await factory.User("Teacher"); var org = teacher.Memberships.Single().OrganisationId;
        var request = Registration(); var path = $"/api/organisations/{org}/devices";
        using var anonymous = factory.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(path, request, CatalogueApiFactory.Json)).StatusCode);
        var created = await teacher.Client.PostAsJsonAsync(path, request, CatalogueApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); Assert.True(created.Headers.CacheControl?.NoStore);
        var registration = (await created.Content.ReadFromJsonAsync<DeviceRegistrationResponse>(CatalogueApiFactory.Json))!;
        Assert.Equal(88, registration.Credential!.Length); Assert.NotEqual(Guid.Empty, registration.Device.Id);
        var duplicate = await teacher.Client.PostAsJsonAsync(path, request, CatalogueApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        var repeated = (await duplicate.Content.ReadFromJsonAsync<DeviceRegistrationResponse>(CatalogueApiFactory.Json))!;
        Assert.Equal(registration.Device.Id, repeated.Device.Id); Assert.Null(repeated.Credential);
        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<MwabuDbContext>().Devices.SingleAsync();
        Assert.NotEqual(registration.Credential, stored.CredentialHash); Assert.Equal(64, stored.CredentialHash.Length);
    }
    [Fact]
    public async Task Device_identifier_alone_is_not_proof_and_credential_rotation_revokes_old_proof()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize();
        var teacher = await factory.User("Teacher"); var org = teacher.Memberships.Single().OrganisationId;
        var registered = (await (await teacher.Client.PostAsJsonAsync($"/api/organisations/{org}/devices", Registration(), CatalogueApiFactory.Json))
            .Content.ReadFromJsonAsync<DeviceRegistrationResponse>(CatalogueApiFactory.Json))!;
        teacher.Client.DefaultRequestHeaders.Add("X-Device-Id", registered.Device.Id.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.Client.GetAsync("/api/devices/current")).StatusCode);
        Proof(teacher.Client, registered);
        Assert.Equal(HttpStatusCode.OK, (await teacher.Client.GetAsync("/api/devices/current")).StatusCode);
        var rotated = (await (await teacher.Client.PostAsync($"/api/organisations/{org}/devices/{registered.Device.Id}/credential", null))
            .Content.ReadFromJsonAsync<DeviceRegistrationResponse>(CatalogueApiFactory.Json))!;
        Assert.NotEqual(registered.Credential, rotated.Credential);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.Client.GetAsync("/api/devices/current")).StatusCode);
        Proof(teacher.Client, rotated);
        Assert.Equal(HttpStatusCode.OK, (await teacher.Client.GetAsync("/api/devices/current")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await teacher.Client.PostAsync($"/api/organisations/{org}/devices/{registered.Device.Id}/revoke", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.Client.GetAsync("/api/devices/current")).StatusCode);
    }
    [Fact]
    public async Task Device_proof_requires_active_membership_and_organisation_and_cannot_cross_organisation()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize();
        var teacher = await factory.User("Teacher"); var membership = teacher.Memberships.Single(); var org = membership.OrganisationId;
        var registered = (await (await teacher.Client.PostAsJsonAsync($"/api/organisations/{org}/devices", Registration(), CatalogueApiFactory.Json))
            .Content.ReadFromJsonAsync<DeviceRegistrationResponse>(CatalogueApiFactory.Json))!;
        Proof(teacher.Client, registered);
        var outsider = await factory.User("Teacher"); Proof(outsider.Client, registered);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.Client.GetAsync("/api/devices/current")).StatusCode);
        teacher.Client.DefaultRequestHeaders.Add("X-Organisation-Id", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.Client.GetAsync("/api/devices/current")).StatusCode);
        teacher.Client.DefaultRequestHeaders.Remove("X-Organisation-Id");
        Assert.Equal(HttpStatusCode.NoContent, (await factory.Admin.PatchAsJsonAsync($"/api/organisations/{org}/members/{membership.Id}/active", new ActiveRequest(false))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.Client.GetAsync("/api/devices/current")).StatusCode);
        await factory.Admin.PatchAsJsonAsync($"/api/organisations/{org}/members/{membership.Id}/active", new ActiveRequest(true));
        await factory.Admin.PatchAsJsonAsync($"/api/organisations/{org}/active", new ActiveRequest(false));
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.Client.GetAsync("/api/devices/current")).StatusCode);
    }
    [Fact]
    public async Task Device_administration_is_bounded_and_registration_validates_input()
    {
        using var factory = new CatalogueApiFactory(); await factory.Initialize();
        var teacher = await factory.User("Teacher"); var org = teacher.Memberships.Single().OrganisationId;
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.Client.GetAsync($"/api/organisations/{org}/devices")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await factory.Admin.GetAsync($"/api/organisations/{org}/devices?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.Client.PostAsJsonAsync($"/api/organisations/{org}/devices",
            new RegisterDeviceRequest(Guid.Empty, "Tablet", DevicePlatform.Android), CatalogueApiFactory.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await teacher.Client.PostAsJsonAsync($"/api/organisations/{org}/devices",
            new RegisterDeviceRequest(Guid.NewGuid(), new string('x', 101), DevicePlatform.Android), CatalogueApiFactory.Json)).StatusCode);
    }
}
