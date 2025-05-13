using Komunikaty;
using MassTransit;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KlientA
{
    public static class ConsoleCol
    {
        public static void WriteLine(string txt, ConsoleColor col = ConsoleColor.White)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = col;
            Console.WriteLine(txt);
            Console.ForegroundColor = prev;
        }
    }

    // ----------- DTO ---------------------------------------------------------
    public class StartZamowienia : IStartZamowienia { public Guid CorrelationId { get; set; } public int ilosc { get; set; } }
    public class Potwierdzenie : IPotwierdzenie { public Guid CorrelationId { get; set; } }
    public class BrakPotwierdzenia : IBrakPotwierdzenia { public Guid CorrelationId { get; set; } }

    // ----------- Handler -----------------------------------------------------
    class HandlerClient :
        IConsumer<IPytanieoPotwierdzenie>,
        IConsumer<IAkceptacjaZamowienia>,
        IConsumer<IOdrzucenieZamowienia>,
        IConsumer<IMagazynNieMaTyle>
    {
        public static readonly HashSet<Guid> MojeZamowienia = new HashSet<Guid>();
        public static Guid? BiezaceId;   // ostatnie pytanie wymagające S/T

        public Task Consume(ConsumeContext<IPytanieoPotwierdzenie> ctx)
        {
            // ignoruj wiadomość, jeśli nie dotyczy naszego zamówienia
            if (!MojeZamowienia.Contains(ctx.Message.CorrelationId))
                return Task.CompletedTask;

            BiezaceId = ctx.Message.CorrelationId;
            ConsoleCol.WriteLine($"[Sklep] Potwierdzasz {ctx.Message.ilosc} szt.? (S/T)", ConsoleColor.Yellow);
            return Task.CompletedTask;
        }

        public Task Consume(ConsumeContext<IAkceptacjaZamowienia> ctx)
        {
            if (MojeZamowienia.Remove(ctx.Message.CorrelationId))
                ConsoleCol.WriteLine("[Sklep] Zamówienie ZAAKCEPTOWANE.", ConsoleColor.Green);
            return Task.CompletedTask;
        }

        public Task Consume(ConsumeContext<IOdrzucenieZamowienia> ctx)
        {
            if (MojeZamowienia.Remove(ctx.Message.CorrelationId))
                ConsoleCol.WriteLine("[Sklep] Zamówienie ODRZUCONE.", ConsoleColor.Red);
            return Task.CompletedTask;
        }

        public Task Consume(ConsumeContext<IMagazynNieMaTyle> ctx)
        {
            if (MojeZamowienia.Remove(ctx.Message.CorrelationId))
                ConsoleCol.WriteLine("[Sklep] Magazyn nie ma tyle produktów!", ConsoleColor.Red);
            return Task.CompletedTask;
        }
    }

    // ----------- Program -----------------------------------------------------
    internal class Program
    {
        static void Main()
        {
            var bus = Bus.Factory.CreateUsingRabbitMq(cfg =>
            {
                var host = cfg.Host(new Uri("rabbitmq://kebnekaise.lmq.cloudamqp.com/hhyacnom"), h =>
                {
                    h.Username("hhyacnom");
                    h.Password("VdxaJdm42N9qqOCcK8gIAJcUoQO-gsF5");
                });

                cfg.ReceiveEndpoint(host, "kolejkaOdbioruA", ep =>
                {
                    ep.Consumer<HandlerClient>();
                });
            });

            bus.Start();
            ConsoleCol.WriteLine("[KlientA] Uruchomiono. Wpisz liczbę sztuk lub S/T/Q.", ConsoleColor.White);

            bool running = true;
            while (running)
            {
                string input = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(input)) continue;

                switch (input.ToUpperInvariant())
                {
                    // ------- potwierdzenie -------
                    case "S":
                        if (HandlerClient.BiezaceId.HasValue)
                        {
                            bus.Publish<IPotwierdzenie>(new Potwierdzenie
                            {
                                CorrelationId = HandlerClient.BiezaceId.Value
                            });
                            ConsoleCol.WriteLine("[KlientA] Wysłano POTWIERDZENIE", ConsoleColor.Green);
                            HandlerClient.BiezaceId = null;
                        }
                        else
                            ConsoleCol.WriteLine("Brak zamówienia do potwierdzenia.", ConsoleColor.Yellow);
                        break;

                    // ------- odmowa -------
                    case "T":
                        if (HandlerClient.BiezaceId.HasValue)
                        {
                            bus.Publish<IBrakPotwierdzenia>(new BrakPotwierdzenia
                            {
                                CorrelationId = HandlerClient.BiezaceId.Value
                            });
                            ConsoleCol.WriteLine("[KlientA] Wysłano BRAK potwierdzenia", ConsoleColor.Red);
                            HandlerClient.BiezaceId = null;
                        }
                        else
                            ConsoleCol.WriteLine("Brak zamówienia do odrzucenia.", ConsoleColor.Yellow);
                        break;

                    // ------- zakończ -------
                    case "Q":
                        running = false;
                        break;

                    // ------- nowe zamówienie -------
                    default:
                        if (int.TryParse(input, out int ilosc) && ilosc > 0)
                        {
                            var id = Guid.NewGuid();
                            bus.Publish<IStartZamowienia>(new StartZamowienia
                            {
                                CorrelationId = id,
                                ilosc = ilosc
                            });
                            HandlerClient.MojeZamowienia.Add(id);
                            ConsoleCol.WriteLine($"[KlientA] Wysłano zamówienie {ilosc} szt. (id={id})", ConsoleColor.Cyan);
                        }
                        else
                            ConsoleCol.WriteLine("Użyj liczby, S, T lub Q.", ConsoleColor.Yellow);
                        break;
                }
            }

            bus.Stop();
        }
    }
}
