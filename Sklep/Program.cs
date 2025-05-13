using Automatonymous;
using Komunikaty;
using MassTransit;
using MassTransit.Saga;
using System;

namespace Sklep
{
    // ---------- sklep ➜ magazyn ----------
    public class PytanieoWolne : IPytanieoWolne
    {
        public Guid CorrelationId { get; set; }
        public int ilosc { get; set; }
    }

    public class AkceptacjaZamowienia : IAkceptacjaZamowienia
    {
        public Guid CorrelationId { get; set; }
        public int ilosc { get; set; }
    }

    public class OdrzucenieZamowienia : IOdrzucenieZamowienia
    {
        public Guid CorrelationId { get; set; }
        public int ilosc { get; set; }
    }

    public class PytanieoPotwierdzenie : IPytanieoPotwierdzenie
    {
        public Guid CorrelationId { get; set; }
        public int ilosc { get; set; }
    }

    public class MagazynNieMaTyle : IMagazynNieMaTyle
    {
        public Guid CorrelationId { get; set; }
    }

    // ------------------ stan ------------------
    public class SklepSagaState : SagaStateMachineInstance
    {
        public Guid CorrelationId { get; set; }     
        public string CurrentState { get; set; }    

        public int ilosc { get; set; }            
    }



    // ------------------ automat ------------------
    public class SklepSaga : MassTransitStateMachine<SklepSagaState>
    {
        // --- stany ---
        public State CzekaNaMagazyn { get; private set; }
        public State CzekaNaKlienta { get; private set; }

        // --- zdarzenia ---
        public Event<IStartZamowienia> StartZamowienia { get; private set; }
        public Event<IOdpowiedzWolne> OdpMagazynOK { get; private set; }
        public Event<IOdpowiedzWolneNegatywna> OdpMagazynNOK { get; private set; }
        public Event<IPotwierdzenie> Potwierdzenie { get; private set; }
        public Event<IBrakPotwierdzenia> BrakPotwierdzenia { get; private set; }


        public SklepSaga()
        {
            InstanceState(x => x.CurrentState);

            // ------------ definicje zdarzeń ------------
            Event(() => StartZamowienia,
                  x => x.CorrelateById(c => c.Message.CorrelationId));

            Event(() => OdpMagazynOK,
                  x => x.CorrelateById(c => c.Message.CorrelationId));

            Event(() => OdpMagazynNOK,
                  x => x.CorrelateById(c => c.Message.CorrelationId));

            Event(() => Potwierdzenie,
                  x => x.CorrelateById(c => c.Message.CorrelationId));

            Event(() => BrakPotwierdzenia,
                  x => x.CorrelateById(c => c.Message.CorrelationId));

            Initially(
        When(StartZamowienia)
        .Then(ctx => ctx.Instance.ilosc = ctx.Data.ilosc)

        // sklep ➜ magazyn
        .Publish(ctx => new PytanieoWolne
        {
            CorrelationId = ctx.Instance.CorrelationId,
            ilosc = ctx.Instance.ilosc
        })

        .TransitionTo(CzekaNaMagazyn)
);

            During(CzekaNaMagazyn,

                When(OdpMagazynOK)          
                    .Publish(ctx => new PytanieoPotwierdzenie
                    {
                        CorrelationId = ctx.Instance.CorrelationId,
                        ilosc = ctx.Instance.ilosc
                    })
                    .TransitionTo(CzekaNaKlienta),

                When(OdpMagazynNOK).Publish(ctx => new MagazynNieMaTyle
                {
                    CorrelationId = ctx.Instance.CorrelationId
                })
                    .Finalize()
            );

            During(CzekaNaKlienta,

                // klient zaakceptował
                When(Potwierdzenie)
                    .Publish(ctx => new AkceptacjaZamowienia     // sklep ➜ klient
                    {
                        CorrelationId = ctx.Instance.CorrelationId,
                        ilosc = ctx.Instance.ilosc
                    })
                    .Finalize(),

                // klient odrzucił
                When(BrakPotwierdzenia)
                    .Publish(ctx => new OdrzucenieZamowienia     // sklep ➜ klient
                    {
                        CorrelationId = ctx.Instance.CorrelationId,
                        ilosc = ctx.Instance.ilosc
                    })
                    .Finalize()
            );

            SetCompletedWhenFinalized();
        }
    }

    internal class Program
    {
        static void Main()
        {
            var repo = new InMemorySagaRepository<SklepSagaState>();

            var saga = new SklepSaga();

            var bus = Bus.Factory.CreateUsingRabbitMq(cfg =>
            {
                cfg.Host(new Uri("rabbitmq://kebnekaise.lmq.cloudamqp.com/hhyacnom"), h =>
                {
                    h.Username("hhyacnom");
                    h.Password("VdxaJdm42N9qqOCcK8gIAJcUoQO-gsF5");
                });

                cfg.ReceiveEndpoint("sklep_saga_queue", ep =>
                {
                    ep.StateMachineSaga(saga, repo);
                });
            });

            bus.Start();
            Console.WriteLine("[Sklep] Saga uruchomiona. Wciśnij Enter, aby zakończyć…");
            Console.ReadLine();
            bus.Stop();
        }
    }
}
