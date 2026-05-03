#nullable enable
using System.Threading.Tasks;
using Scaffold.GraphFlow.M0;

namespace Scaffold.GraphFlow.CardSandbox.Cards
{
    /// <summary>
    /// 500 Strike entry payload. In the full M3 setup this carries `[GraphEntry]` and the
    /// generator emits the entry editor + runtime nodes from it; in this sandbox the runtime
    /// node (<see cref="Strike500EntryRuntime"/>) is hand-written for the test fixture.
    /// </summary>
    public sealed class Strike500
    {
        /// <summary>Base damage delivered by the card before listeners modify it.</summary>
        public int BaseDamage = 5;
    }

    /// <summary>Mode-2 command — deals N damage. Listeners can rewrite the result before it returns.</summary>
    public sealed class DealDamageCommand : Command<DamageResult>
    {
        public int Amount;

        public override Task<DamageResult> Execute(IEffectScope scope) =>
            Task.FromResult(new DamageResult { DamageDealt = Amount });
    }

    public struct DamageResult
    {
        public int DamageDealt;
    }

    /// <summary>
    /// Hand-written entry runtime for <see cref="Strike500"/>. Stamps the card's base damage onto
    /// an <see cref="OutputPort{Int32}"/> the dispatcher node reads via Connection.Bind.
    /// </summary>
    public sealed class Strike500EntryRuntime : EntryRuntimeNode<Strike500, CardEffectRunner>
    {
        public const int FlowOutPortId  = unchecked((int)0xC0010001u);
        public const int BaseDamagePort = unchecked((int)0xC0010002u);

        public OutputPort<int> BaseDamage = null!;

        int _baseDamageValue;

        public Strike500EntryRuntime()
        {
            BaseDamage = new OutputPort<int>(() => _baseDamageValue);
            Ports.Add(BaseDamagePort, BaseDamage);
        }

        public override Task<FlowContinuation> Execute(CardEffectRunner runner)
        {
            if (Payload != null)
            {
                _baseDamageValue = Payload.BaseDamage;
            }
            return Task.FromResult(FlowContinuation.Next(FlowOutPortId));
        }
    }

    /// <summary>
    /// Hand-written dispatcher node for <see cref="DealDamageCommand"/>. In the full M3 path the
    /// generator emits the per-payload <c>BuildPayload</c> + <c>WriteOutputs</c> pair from the
    /// command's typed fields; here we wire them directly so the test can exercise the pipeline +
    /// listener chain without standing up the editor + generator scaffolding.
    /// </summary>
    public sealed class DealDamageDispatcherRuntime : CardCommandDispatcher<DealDamageCommand, DamageResult>
    {
        public const int FlowInPortIdConst   = unchecked((int)0xC0020001u);
        public const int FlowOutPortIdConst  = unchecked((int)0xC0020002u);
        public const int AmountPortId        = unchecked((int)0xC0020003u);
        public const int DamageDealtPortId   = unchecked((int)0xC0020004u);

        public InputPort<int>  Amount      = null!;
        public OutputPort<int> DamageDealt = null!;

        int _damageDealtValue;

        protected override int FlowOutPortId => FlowOutPortIdConst;

        public DealDamageDispatcherRuntime()
        {
            Amount      = new InputPort<int>();
            DamageDealt = new OutputPort<int>(() => _damageDealtValue);
            Ports.Add(AmountPortId,      Amount);
            Ports.Add(DamageDealtPortId, DamageDealt);
        }

        protected override DealDamageCommand BuildPayload(CardEffectRunner runner) =>
            new DealDamageCommand { Amount = Amount.Read() };

        protected override void WriteOutputs(DamageResult result) =>
            _damageDealtValue = result.DamageDealt;
    }
}
