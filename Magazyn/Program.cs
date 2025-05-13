using Komunikaty;
using MassTransit;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Magazyn
{
    public static class ConsoleCol
    {
        public static void WriteLine(string text, ConsoleColor color = ConsoleColor.White)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = prev;
        }
    }

    #region Komunikaty wychodzące z magazynu
    public class OdpowiedzWolne : IOdpowiedzWolne
    {
        public Guid CorrelationId { get; set; }
        public int ilosc { get; set; }
    }

    public class OdpowiedzNegatywna : IOdpowiedzWolneNegatywna
    {
        public Guid CorrelationId { get; set; }
        public int ilosc { get; set; }
    }
    #endregion

    public class MagazynStan
    {
        private readonly object _locker = new object();
        private int _wolne = 0;
        private int _zarezerwowane = 0;

        public (int wolne, int zarezerwowane) PobierzStan()
        {
            lock (_locker) return (_wolne, _zarezerwowane);
        }

        public void DodajWolne(int ilosc)
        {
            lock (_locker) _wolne += ilosc;
        }

        /// <summary>Zarezerwuj – zwraca true jeśli się udało.</summary>
        public bool Rezerwuj(int ilosc)
        {
            lock (_locker)
            {
                if (_wolne < ilosc) return false;
                _wolne -= ilosc;
                _zarezerwowane += ilosc;
                return true;
            }
        }

        /// <summary>Usuwa zarezerwowane (po akceptacji zamówienia).</summary>
        public void Zuzyj(int ilosc)
        {
            lock (_locker) _zarezerwowane -= ilosc;
        }

        /// <summary>Porzuca rezerwację i zwraca towar na wolne.</summary>
        public void Zwroc(int ilosc)
        {
            lock (_locker)
            {
                _zarezerwowane -= ilosc;
                _wolne += ilosc;
            }
        }
    }

    /// <summary>Konsument wiadomości przychodzących od sklepu.</summary>
    public class HandlerMagazyn :
        IConsumer<IPytanieoWolne>,
        IConsumer<IAkceptacjaZamowienia>,
        IConsumer<IOdrzucenieZamowienia>
    {
        private readonly MagazynStan _stan;

        public HandlerMagazyn(MagazynStan stan) => _stan = stan;

        public async Task Consume(ConsumeContext<IPytanieoWolne> ctx)
        {
            int ilosc = ctx.Message.ilosc;
            bool ok = _stan.Rezerwuj(ilosc);

            if (ok)
            {
                await ctx.Publish<IOdpowiedzWolne>(new OdpowiedzWolne { CorrelationId = ctx.Message.CorrelationId, ilosc = ilosc });
                ConsoleCol.WriteLine($"[Magazyn] +Rezerwacja {ilosc} - OK", ConsoleColor.Green);
            }
            else
            {
                await ctx.Publish<IOdpowiedzWolneNegatywna>(new OdpowiedzNegatywna { CorrelationId = ctx.Message.CorrelationId, ilosc = ilosc });
                ConsoleCol.WriteLine($"[Magazyn] Brak wolnych sztuk dla {ilosc} - NEGATYW", ConsoleColor.Red);
            }

            WypiszStan();
        }

        public Task Consume(ConsumeContext<IAkceptacjaZamowienia> ctx)
        {
            _stan.Zuzyj(ctx.Message.ilosc);
            ConsoleCol.WriteLine($"[Magazyn] Zamówienie zaakceptowane – zużyto {ctx.Message.ilosc}", ConsoleColor.Cyan);
            WypiszStan();
            return Task.CompletedTask;
        }

        public Task Consume(ConsumeContext<IOdrzucenieZamowienia> ctx)
        {
            _stan.Zwroc(ctx.Message.ilosc);
            ConsoleCol.WriteLine($"[Magazyn] Zamówienie odrzucone – zwrot {ctx.Message.ilosc}", ConsoleColor.Yellow);
            WypiszStan();
            return Task.CompletedTask;
        }

        private void WypiszStan()
        {
            var (wolne, zarezerwowane) = _stan.PobierzStan();
            ConsoleCol.WriteLine($"   >>> STAN: wolne = {wolne}, zarezerwowane = {zarezerwowane}", ConsoleColor.White);
        }
    }

    internal class Program
    {
        static void Main()
        {
            var stan = new MagazynStan();

            var bus = Bus.Factory.CreateUsingRabbitMq(cfg =>
            {
                cfg.Host(new Uri("rabbitmq://kebnekaise.lmq.cloudamqp.com/hhyacnom"), h =>
                {
                    h.Username("hhyacnom");
                    h.Password("VdxaJdm42N9qqOCcK8gIAJcUoQO-gsF5");
                });

                cfg.ReceiveEndpoint("magazyn_queue", e =>
                {
                    e.Consumer(() => new HandlerMagazyn(stan));
                });
            });

            bus.Start();
            ConsoleCol.WriteLine("[Magazyn] Uruchomiono. Podaj liczbę, aby dodać towar; Q aby wyjść.", ConsoleColor.White);

            bool running = true;
            while (running)
            {
                string input = Console.ReadLine()?.Trim();
                if (input == null || input.Length == 0) continue;

                if (input.Equals("Q", StringComparison.OrdinalIgnoreCase))
                {
                    running = false;
                    continue;
                }

                if (int.TryParse(input, out int ilosc) && ilosc > 0)
                {
                    stan.DodajWolne(ilosc);
                    ConsoleCol.WriteLine($"[Magazyn] +{ilosc} do wolnych", ConsoleColor.Green);
                    var (wolne, zarezerwowane) = stan.PobierzStan();
                    ConsoleCol.WriteLine($"   >>> STAN: wolne = {wolne}, zarezerwowane = {zarezerwowane}", ConsoleColor.White);
                }
                else
                {
                    ConsoleCol.WriteLine("Błędna komenda – wpisz liczbę dodatnią albo Q.", ConsoleColor.Red);
                }
            }

            bus.Stop();
        }
    }
}
