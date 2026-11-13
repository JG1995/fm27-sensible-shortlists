using SensibleShortlists.Core.Triage;

namespace SensibleShortlists.Core.Tests.Triage;

public class ReadLedgerTests
{
    private readonly ReadLedger _ledger = new(patienceInTicks: 3);
    private readonly List<string> _answers = new();

    private void Expect(ulong key) => _ledger.Expect(key, value => _answers.Add($"{key}={value?.Text ?? "null"}"));

    [Fact]
    public void An_expected_answer_reaches_whoever_asked()
    {
        Expect(1);

        Assert.True(_ledger.Answer(1, new GameValue("True")));
        Assert.Equal(new[] { "1=True" }, _answers);
    }

    [Fact]
    public void An_answer_nobody_asked_for_is_not_taken()
    {
        Assert.False(_ledger.Answer(1, new GameValue("True")));
        Assert.Empty(_answers);
    }

    [Fact]
    public void A_read_is_expected_until_it_is_answered()
    {
        Expect(1);

        Assert.True(_ledger.IsExpected(1));
        _ledger.Answer(1, null);
        Assert.False(_ledger.IsExpected(1));
    }

    [Fact]
    public void A_second_answer_to_the_same_read_is_not_taken()
    {
        Expect(1);
        _ledger.Answer(1, new GameValue("True"));

        Assert.False(_ledger.Answer(1, new GameValue("False")));
        Assert.Equal(new[] { "1=True" }, _answers);
    }

    [Fact]
    public void A_read_the_game_never_answers_is_given_up_as_unreadable_when_patience_runs_out()
    {
        Expect(1);

        Assert.Empty(_ledger.Tick());
        Assert.Empty(_ledger.Tick());
        Assert.Equal(new ulong[] { 1 }, _ledger.Tick());

        Assert.Equal(new[] { "1=null" }, _answers);
    }

    [Fact]
    public void A_read_that_was_given_up_takes_no_late_answer()
    {
        Expect(1);
        _ledger.Tick();
        _ledger.Tick();
        _ledger.Tick();

        Assert.False(_ledger.Answer(1, new GameValue("True")));
    }

    [Fact]
    public void Each_read_has_its_own_patience()
    {
        Expect(1);
        _ledger.Tick();
        Expect(2);
        _ledger.Tick();

        Assert.Equal(new ulong[] { 1 }, _ledger.Tick());
        Assert.Equal(new ulong[] { 2 }, _ledger.Tick());
    }

    [Fact]
    public void An_answered_read_is_never_given_up()
    {
        Expect(1);
        _ledger.Answer(1, new GameValue("True"));

        Assert.Empty(_ledger.Tick());
        Assert.Empty(_ledger.Tick());
        Assert.Empty(_ledger.Tick());
        Assert.Single(_answers);
    }
}
