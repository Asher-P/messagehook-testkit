using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MessageHook.EchoService.Tracking;
using Microsoft.Extensions.Logging.Abstractions;

namespace MessageHook.NUnit;

/// <summary>
/// Broker-free tests for the echo service's change detection: does a re-sent id with a new name come back with
/// <c>IsChanged</c> true, and does an unchanged one come back false.
/// </summary>
public class EchoChangeDetectionTests
{
    private PayloadChangeStamper _stamper = null!;

    [SetUp]
    public void SetUp() =>
        _stamper = new PayloadChangeStamper(new MessageChangeTracker(), NullLogger<PayloadChangeStamper>.Instance);

    private static byte[] Animal(string id, string name) =>
        Encoding.UTF8.GetBytes($$"""{ "id": "{{id}}", "name": "{{name}}" }""");

    private static bool? IsChangedOf(byte[] payload) =>
        JsonNode.Parse(payload)?[PayloadChangeStamper.IsChangedField]?.GetValue<bool>();

    [Test]
    public void First_sighting_of_an_id_is_not_a_change()
    {
        Assert.That(IsChangedOf(_stamper.Stamp(Animal("1", "cat"), "k")), Is.False);
    }

    [Test]
    public void Same_id_with_a_new_name_is_a_change()
    {
        _stamper.Stamp(Animal("1", "cat"), "k");
        Assert.That(IsChangedOf(_stamper.Stamp(Animal("1", "dog"), "k")), Is.True);
    }

    [Test]
    public void Same_id_with_the_same_name_is_not_a_change()
    {
        _stamper.Stamp(Animal("1", "cat"), "k");
        _stamper.Stamp(Animal("1", "dog"), "k");
        Assert.That(IsChangedOf(_stamper.Stamp(Animal("1", "dog"), "k")), Is.False);
    }

    [Test]
    public void Names_are_tracked_per_id_not_globally()
    {
        _stamper.Stamp(Animal("1", "cat"), "k");
        // A different id seeing a different name is still its own first sighting.
        Assert.That(IsChangedOf(_stamper.Stamp(Animal("2", "dog"), "k")), Is.False);
    }

    [Test]
    public void An_inbound_IsChanged_flag_is_overwritten_not_trusted()
    {
        var payload = Encoding.UTF8.GetBytes("""{ "id": "1", "name": "cat", "IsChanged": true }""");
        Assert.That(IsChangedOf(_stamper.Stamp(payload, "k")), Is.False);
    }

    [Test]
    public void An_inbound_flag_in_another_casing_does_not_survive_as_a_duplicate()
    {
        var payload = Encoding.UTF8.GetBytes("""{ "id": "1", "name": "cat", "ischanged": true }""");
        var echoed = JsonNode.Parse(_stamper.Stamp(payload, "k"))!.AsObject();

        Assert.That(echoed.Count(p => p.Key.Equals("IsChanged", StringComparison.OrdinalIgnoreCase)), Is.EqualTo(1));
        Assert.That(echoed[PayloadChangeStamper.IsChangedField]!.GetValue<bool>(), Is.False);
    }

    [Test]
    public void The_rest_of_the_payload_is_echoed_untouched()
    {
        var payload = Encoding.UTF8.GetBytes("""{ "id": "1", "name": "cat", "owner": { "city": "Haifa" } }""");
        var echoed = JsonNode.Parse(_stamper.Stamp(payload, "k"))!;

        Assert.Multiple(() =>
        {
            Assert.That(echoed["id"]!.GetValue<string>(), Is.EqualTo("1"));
            Assert.That(echoed["name"]!.GetValue<string>(), Is.EqualTo("cat"));
            Assert.That(echoed["owner"]!["city"]!.GetValue<string>(), Is.EqualTo("Haifa"));
        });
    }

    [Test]
    public void Id_and_name_are_matched_case_insensitively()
    {
        _stamper.Stamp(Encoding.UTF8.GetBytes("""{ "Id": "1", "Name": "cat" }"""), "k");
        var echoed = _stamper.Stamp(Encoding.UTF8.GetBytes("""{ "Id": "1", "Name": "dog" }"""), "k");
        Assert.That(IsChangedOf(echoed), Is.True);
    }

    [Test]
    public void The_message_key_identifies_the_animal_when_the_payload_has_no_id()
    {
        _stamper.Stamp(Encoding.UTF8.GetBytes("""{ "name": "cat" }"""), "animal-1");
        var echoed = _stamper.Stamp(Encoding.UTF8.GetBytes("""{ "name": "dog" }"""), "animal-1");
        Assert.That(IsChangedOf(echoed), Is.True);
    }

    [Test]
    public void A_payload_that_is_not_a_json_object_is_echoed_byte_for_byte()
    {
        var notJson = Encoding.UTF8.GetBytes("not json at all");
        var array = Encoding.UTF8.GetBytes("[1, 2, 3]");

        Assert.Multiple(() =>
        {
            Assert.That(_stamper.Stamp(notJson, "k"), Is.EqualTo(notJson));
            Assert.That(_stamper.Stamp(array, "k"), Is.EqualTo(array));
        });
    }

    [Test]
    public void The_scenario_from_the_playbook_seed_cat_then_rename_to_dog()
    {
        // produce-only seed, then the rename that the playbook asserts on, then a no-op resend.
        Assert.Multiple(() =>
        {
            Assert.That(IsChangedOf(_stamper.Stamp(Animal("1", "cat"), "1")), Is.False, "seed");
            Assert.That(IsChangedOf(_stamper.Stamp(Animal("1", "dog"), "1")), Is.True, "renamed");
            Assert.That(IsChangedOf(_stamper.Stamp(Animal("1", "dog"), "1")), Is.False, "resent");
        });
    }

    [Test]
    public void Concurrent_messages_for_one_id_report_exactly_one_change()
    {
        // Two workers can stamp at once; read-then-write must not let both see the old name.
        var tracker = new MessageChangeTracker();
        tracker.RecordAndDetectChange("1", "cat");

        var changes = 0;
        Parallel.For(0, 200, _ =>
        {
            if (tracker.RecordAndDetectChange("1", "dog"))
                Interlocked.Increment(ref changes);
        });

        Assert.That(changes, Is.EqualTo(1));
    }
}
