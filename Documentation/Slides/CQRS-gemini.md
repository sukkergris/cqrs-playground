---
marp: true
theme: default
title: Introduktion til CQRS & Mediator Pattern med LiteBus
style: |
  section {
    font-size: 1.5em;
  }
  h1, h2, h3 {
    font-size: 2em;
  }
---

# Introduktion til CQRS & Mediator Pattern med LiteBus
En praktisk guide til renere og mere skalerbar kode  
Præsenteret af: [Dit Navn]

---

## Agenda
- **Del 1: Teori (Hvorfor?)**
  - Problemet med traditionel arkitektur
  - Hvad er CQRS?
  - Myte-aflivning: CQRS vs. Event Sourcing
  - Beskeder: Commands, Queries & Events
- **Del 2: Praksis (Hvordan?)**
  - Introduktion til LiteBus
  - Best Practice: Regler for Handlers
  - Commands vs. DTOs
  - Middleware
- **Del 3: Diskussion & Opsamling**
  - Evolutionært Design: Hvornår er det det værd?

---

## Problemet: Den traditionelle "Fat Controller"
Vi kender det alle sammen. En controller-metode, der gør alt.

```c#
// UserController.cs
public IActionResult UpdateUser(int id, UserViewModel model)
{
    // 1. Hent data
    var userEntity = _userService.GetUserById(id);
    if (userEntity == null) return NotFound();

    // 2. Validering
    if (model.Email != userEntity.Email && _userService.EmailExists(model.Email))
    {
        ModelState.AddModelError("Email", "Email already exists.");
        return BadRequest(ModelState);
    }
    
    // 3. Map fra ViewModel til Entity
    userEntity.Name = model.Name;
    userEntity.Email = model.Email;

    // 4. Gem til database
    _unitOfWork.SaveChanges();

    return NoContent();
}
```

---

## Et pattern at overveje: CQRS
**Command Query Responsibility Segregation**

Princippet er simpelt:
- Adskil ansvaret for at ændre data fra ansvaret for at læse data.
- Man enten giver en ordre (Command) eller stiller et spørgsmål (Query). Aldrig begge dele på én gang.

---

## Myte: "Skal jeg bruge Event Sourcing for at lave CQRS?"
❌ Nej! Dette er en af de største misforståelser.

**CQRS:**
- Handler om at adskille læse- og skrive-modellerne i din applikation.
- Du kan opnå KÆMPE fordele med CQRS alene, uden Event Sourcing.

**Event Sourcing:**
- Handler om, hvordan du gemmer data. I stedet for at gemme den nuværende tilstand, gemmer du en log af alle hændelser (events).
- Kræver CQRS for at være skalerbart, men det omvendte er ikke sandt.

**Konklusion:** Start med CQRS. Overvej kun Event Sourcing, hvis I har et specifikt behov for det (f.eks. fuld revisionshistorik).

---

## Systemet som en "Message Bus"
I en CQRS-arkitektur kommunikerer vi med systemet via beskeder (Messages).

- **Commands:** Klienten beder applikationen om at udføre en handling.
- **Queries:** Klienten spørger applikationen om data.
- **Events:** Applikationen informerer omverdenen om, at noget er sket.

---

## Navngivning er altafgørende
En klar navngivning gør koden let at læse og forstå.

- **Commands (Ordre):** Brug bydeform. De udtrykker en intention.
  - `CreateProductCommand`, `UpdateUserAddressCommand`
- **Queries (Spørgsmål):** Start typisk med "Get".
  - `GetProductByIdQuery`, `GetActiveOrdersQuery`
- **Events (Fakta):** Brug datid. De beskriver noget, der er sket.
  - `ProductCreatedEvent`, `UserAddressUpdatedEvent`

---

## Vigtig forskel: Command vs. Event
- **Command (en anmodning):**
  - "Jeg vil gerne opdatere brugerens adresse."
  - Kan afvises. Systemet kan validere anmodningen og sige "Nej, det kan du ikke."
- **Event (et faktum):**
  - "Brugerens adresse ER BLEVET opdateret."
  - Kan ikke afvises. Det er en konstatering af noget, der allerede er sket.

---

## Myte: "Commands skal ikke returnere noget" (One-way commands)
❌ Dette er en anden sejlivet myte. Det er helt i orden for en Command Handler at returnere et resultat.

**Sandheden:** En Command er en handling. Klienten har ofte brug for at vide, hvad resultatet af handlingen var.

**God praksis:** Returner simple værdier, der bekræfter success.
- ID'et på den nye ressource: `return newProductId;`
- En status: `return Result.Success();`
- En reference til den oprettede ressource.

Undgå at returnere komplekse domæneobjekter, men at returnere ingenting er ofte upraktisk.

---

## Kaffepause (10 min)

---

# DEL 2: PRAKSIS
Let's write some code!

---

## Introduktion til LiteBus
Et moderne, modulært og højtydende Mediator-bibliotek til .NET.

- **Modulært:** Du installerer kun de dele, du har brug for.
- **Høj Ydeevne:** Designet til at være hurtigt.
- **Fleksibelt:** Let at udvide med custom middleware.

**Installation (for Commands & Queries):**
```sh
dotnet add package LiteBus.Core.Mediation
dotnet add package LiteBus.Commands.Core
dotnet add package LiteBus.Queries.Core
```

---

## Best Practice: Regler for Handlers
For at holde systemet simpelt og forudsigeligt, er der nogle simple regler:

| Fra              | Til en Command Handler? | Til en Query Handler? |
|------------------|------------------------|----------------------|
| Command Handler  | NEJ!                   | Ja (for at hente data) |
| Query Handler    | NEJ!                   | Ja (genbrug af simple queries) |

- En Command Handler må **ALDRIG** kalde en anden Command. Dette skaber uforudsigelige kæder af handlinger. Hvis flere ting skal ske, skal det orkestreres af domæne-events eller en højere-niveau proces.
- En Query Handler må **ALDRIG** kalde en Command. Queries må ikke ændre systemets tilstand.

---

## Commands vs. DTOs: En vigtig sondring
Selvom de ligner hinanden, løser de forskellige problemer.

**Command**
- Formål: En serialiserbar "metodekald". En handling, der skal udføres.
- Kobling: Tæt koblet til din interne domænelogik.
- Levetid: Bør refaktoreres ofte, som din logik ændrer sig.

**DTO (Data Transfer Object)**
- Formål: En datakontrakt med omverdenen (f.eks. et frontend).
- Kobling: Tæt koblet til en specifik klient eller API-version.
- Levetid: Skal være bagudkompatibel.

> At bruge et Command direkte som DTO er fint, hvis du kontrollerer både klient og server og kan deploye dem samtidigt.

---

## Avanceret: Middleware
I LiteBus hedder pipeline-elementer Middleware. De "pakker" din Handler ind i ekstra logik.

Dette er perfekt til Cross-Cutting Concerns som validering, logging, transaktioner og caching.

---

## Mediator Pattern: Afkobling og struktur
Mediator patternet er et designmønster, der hjælper med at afkoble afsendere og modtagere af beskeder i et system.

- **Formål:** Gør det muligt for objekter at kommunikere uden at kende direkte til hinanden.
- **Hvordan?** Alle beskeder (commands, queries, events) sendes til en central mediator, som sørger for at dirigere dem til den rette handler.
- **Fordele:**
  - Mindre kobling mellem komponenter
  - Lettere at udvide og teste
  - Giver et centralt sted til cross-cutting concerns (logging, validering, transaktioner)

I .NET kan mediator patternet implementeres med biblioteker som LiteBus eller MediatR.

---

# DEL 3: DISKUSSION & OPSAMLING

## Evolutionært Design: Anvend mønstre med omhu
CQRS er et værktøj, ikke en religion. Start altid simpelt og tilføj kun kompleksitet, når det er nødvendigt.

- Anvend kun et mønster, når fordelene tydeligt overstiger omkostningerne.
- Start simpelt: En Command Handler kan sagtens bruge det samme DbContext og de samme domænemodeller som en Query Handler.
- Udvid senere: Hvis I oplever performance-problemer på læse-siden, så kan I begynde at optimere med f.eks. Dapper og separate read-modeller.

---

## Opsamling
- CQRS adskiller læsning (Query) fra skrivning (Command). Det er ikke det samme som Event Sourcing.
- Beskeder (Commands, Queries, Events) er kernen. Husk de klare navnekonventioner og forskellen på en anmodning og et faktum.
- Mediator afkobler afsender fra modtager. LiteBus er et godt, moderne valg.
- Best Practices: Undgå at kalde commands fra handlers, og husk at commands gerne må returnere simple resultater.
- Start simpelt! Anvend kun mønsteret, hvor kompleksiteten retfærdiggør det.

---

## Lær mere
- **LiteBus GitHub (dokumentation og eksempler):**  
  [github.com/Ali-Rezaei/LiteBus](https://github.com/Ali-Rezaei/LiteBus)
- **CQRS in Practice (Pluralsight-kursus af Vladimir Khorikov):**  
  [app.pluralsight.com/library/courses/cqrs-in-practice/table-of-contents](https://app.pluralsight.com/library/courses/cqrs-in-practice/table-of-contents)
- **Martin Fowler om CQRS:**  
  [martinfowler.com/bliki/CQRS.html](https://martinfowler.com/bliki/CQRS.html)

---

# Q&A
Spørgsmål?
Tak for i dag!