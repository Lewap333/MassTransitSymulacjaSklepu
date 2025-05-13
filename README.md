# MassTransit Symulacja Sklepu

Symulacja wielo‑usługowego systemu **sklep ⇄ magazyn ⇄ klienci** zbudowanego na komunikatach **MassTransit.RabbitMQ 6.2.4**.

---

## Spis treści

1. [Architektura](#Architektura)
2. [Przepływ komunikatów](#Przepływ-komunikatów)
3. [Wymagania](#Wymagania)
4. [Konfiguracja RabbitMQ](#Konfiguracja-rabbitmq)
6. [Sterowanie z konsoli](#Sterowanie-z-konsoli)
7. [Rozszerzenia i pomysły](#Rozszerzenia-i-pomysły)
8. [Przykład działania](Przykład-działania)

---

## Architektura

```
┌────────┐          ┌────────┐          ┌──────────┐
│KlientA │          │        │          │          │
│KlientB │──Start──▶│  Sklep │──QA/OK──▶│ Magazyn  │
│  ...   │◀─(PO)────│  (Saga)│◀─Odp─────│          │
└────────┘          └────────┘          └──────────┘
```

* **Klienci** wysyłają `StartZamowienia` i później `Potwierdzenie` / `BrakPotwierdzenia`.
* **Sklep** to **saga**; rezerwuje towar w magazynie i negocjuje z klientem.
* **Magazyn** utrzymuje stan wolnych / zarezerwowanych jednostek i odpowiada `OdpowiedzWolne` lub `OdpowiedzWolneNegatywna`.
* Wszystkie wiadomości mają wspólny `CorrelationId` (`Guid`) – MassTransit automatycznie koreluje zdarzenia z instancją sagi.

## Przepływ komunikatów

```
Klient          Sklep (saga)             Magazyn
  | StartZamowienia (id) |              |
  |─────────────────────▶|              |
  |                      | PytanieWolne |
  |                      |─────────────▶|
  |                      |  (OK/NOK)    |
  |                      |◀─────────────|
  | Potwierdzenie /      |              |
  | BrakPotwierdzenia    |◀─────────────|
  |─────────────────────▶|              |
  |                      | Akceptacja / |
  |                      | Odrzucenie   |
  |                      |─────────────▶|
```

## Wymagania

* **.NET Framework 4.8** lub **.NET 6/8 SDK** *(konsolowe aplikacje)*
* **MassTransit.RabbitMQ 6.2.4** *(NuGet)*
* **Konto w CloudAMQP** – darmowy plan *Little Lemur* w zupełności wystarczy (hostuje naszego brokera RabbitMQ)

## Konfiguracja RabbitMQ

Projekt zawiera **twardo zakodowane** dane przykładowego konta CloudAMQP w plikach `Program.cs` każdego mikroserwisu (`Sklep`, `Magazyn`, `KlientA`, `KlientB`).

> **Przed uruchomieniem w swoim środowisku** otwórz te pliki i zamień:
>
> ```csharp
> "rabbitmq://kebnekaise.lmq.cloudamqp.com/hhyacnom"
> h.Username("hhyacnom");
> h.Password("VdxaJdm42N9qqOCcK8gIAJcUoQO-gsF5");
> ```
>
> na URI, nazwę użytkownika i hasło z Twojego własnego konta CloudAMQP.

## Sterowanie z konsoli

| Aplikacja   | Klawisze / polecenia | Działanie                  |
| ----------- | -------------------- | -------------------------- |
| **Klienci** | `liczba` + Enter     | wysyła `StartZamowienia`   |
|             | `S`                  | wysyła `Potwierdzenie`     |
|             | `T`                  | wysyła `BrakPotwierdzenia` |
|             | `Q`                  | zamyka aplikację           |
| **Magazyn** | `liczba` + Enter     | dodaje jednostki **wolne** |
|             | `Q`                  | zamyka aplikację           |

Stan magazynu drukuje się po każdej zmianie (wolne / zarezerwowane).

## Rozszerzenia i pomysły

* **Timeout** w sadze – automatyczne odrzucenie, gdy klient nie odpowie w X s.
---

Projekt stworzony w celach edukacyjnych – pokazuje, jak budować procesy długo‑trwające z pomocą **MassTransit 6.x**, RabbitMQ i wzorca **Saga**.

## Przykład działania
![image](https://github.com/user-attachments/assets/ff4ec013-68ea-488b-a04e-29d2dd0b51d6)

