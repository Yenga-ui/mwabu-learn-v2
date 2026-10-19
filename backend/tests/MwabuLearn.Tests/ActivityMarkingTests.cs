using MwabuLearn.Application.Content;
namespace MwabuLearn.Tests;
public sealed class ActivityMarkingTests
{
    private static ActivityDefinition Definition(string kind) => new(2, kind, "Mathematics", "Explore and answer", "en", kind is "matching" or "sequencing" or "fill-blank" ? [new("Complete", ["one", "two"], 0, "Count carefully")] : [], 2, 0, 10, 4);
    [Theory]
    [InlineData("matching", "0")]
    [InlineData("fill-blank", " ONE ")]
    [InlineData("number-line", "2")]
    [InlineData("fraction", "2")]
    public void Server_marks_bounded_answers(string kind, string answer)
    {
        var definition = Definition(kind); Assert.Equal(1, ActivityRules.Mark(definition, [[answer]]).Score);
        Assert.Equal(0, ActivityRules.Mark(definition, [["wrong"]]).Score);
    }
    [Fact]
    public void A_blank_can_have_one_accepted_answer_without_quiz_distractors()
    {
        var definition=Definition("fill-blank") with {Questions=[new("Name one half",["1/2"],0,"One of two equal parts.")]};
        Assert.Equal(1,ActivityRules.Mark(definition,[["1/2"]]).Score);Assert.Equal(0,ActivityRules.Mark(definition,[["1/3"]]).Score);
    }
    [Fact]
    public void Sequence_and_place_values_require_all_positions_in_order()
    {
        Assert.Equal(1, ActivityRules.Mark(Definition("sequencing"), [["0", "1"]]).Score);
        Assert.Equal(0, ActivityRules.Mark(Definition("sequencing"), [["1", "0"]]).Score);
        Assert.Equal(1, ActivityRules.Mark(Definition("place-value-exercise"), [["0", "0", "0", "2"]]).Score);
        Assert.Equal(0, ActivityRules.Mark(Definition("place-value-exercise"), [["2", "0", "0", "0"]]).Score);
    }
    [Fact]
    public void Invalid_versions_ranges_fractions_and_answer_shapes_are_rejected()
    {
        foreach(var invalid in new[] { Definition("fraction") with { Number=5 }, Definition("number-line") with {Maximum=101}, Definition("matching") with {SchemaVersion=1} }) Assert.Throws<ContentException>(() => ActivityRules.Validate(invalid));
        Assert.Throws<ContentException>(() => ActivityRules.Mark(Definition("matching"), []));
        Assert.Throws<ContentException>(() => ActivityRules.Mark(Definition("matching"), [[new string('a',301)]]));
    }
}
