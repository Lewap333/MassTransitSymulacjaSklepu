using System;
using MassTransit;

namespace Komunikaty
{
    public interface ICorrelated : CorrelatedBy<Guid> { }

    // ----------- klient ➜ sklep ----------
    public interface IStartZamowienia : ICorrelated { int ilosc { get; } }

    // ----------- sklep ➜ klient ----------
    public interface IPytanieoPotwierdzenie : ICorrelated { int ilosc { get; } }
    public interface IAkceptacjaZamowienia : ICorrelated { int ilosc { get; } }
    public interface IOdrzucenieZamowienia : ICorrelated { int ilosc { get; } }

    // ----------- klient ➜ sklep ----------
    public interface IPotwierdzenie : ICorrelated { }
    public interface IBrakPotwierdzenia : ICorrelated { }

    // ----------- sklep ➜ magazyn ----------
    public interface IPytanieoWolne : ICorrelated { int ilosc { get; } }

    // ----------- magazyn ➜ sklep ----------
    public interface IOdpowiedzWolne : ICorrelated { }
    public interface IOdpowiedzWolneNegatywna : ICorrelated { }

    public interface IMagazynNieMaTyle : ICorrelated { }
}
