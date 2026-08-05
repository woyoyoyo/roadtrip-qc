# roadtrip-qc 🍁

PWA de planning pour notre roadtrip au Québec — Blazor WASM + GitHub Pages.
Les données vivent dans un **Gist GitHub** (`roadtrip.json`), l'app est déployée sur **GitHub Pages**.

## Pages

- `/` — **Planning** jour par jour (hébergement + activités), compte à rebours, export PDF
- `/reservations` — **Réservations** : où on dort et tout ce qu'il faut réserver
  (à réserver 🔖 / réservé ✅ / payé 💳 + budget, catégorie, réf., contact)
- `/idees` — **Idées** de trucs à faire, filtrables par étape/région (idée 💡 / prévu 📌 / fait ✅)
- `/contacts` — **Contacts** utiles (hôtes, loueurs, restos, guides)
- `/assistant` — **Assistant IA** (Gemini) : ajoute une réservation, une idée ou une activité
  en langage naturel (avec dictée vocale)
- `/checklist` — liste à emporter / à préparer (accès depuis Paramètres)
- `/parametres` — ID du Gist + token GitHub (PAT) + clé Gemini, stockés en localStorage uniquement

Ces concepts (réservations, idées, contacts, assistant IA) sont transposés du projet sœur
[Mariage Planning](https://github.com/woyoyoyo/mariage-planning) (matériel → réservations, todos → idées).

## Fonctionnement

- **Lecture** : API GitHub (Gist) → cache localStorage → données de démo
- **Écriture** : fetch-avant-PATCH — on relit toujours la dernière version du Gist avant
  d'appliquer une modification, pour ne pas écraser les changements d'un autre téléphone

## Modèle de données (`roadtrip.json`)

En plus de `trip`, `parts`, `days` et `checklist`, le fichier porte trois nouvelles listes :

```json
{
  "reservations": [
    { "id": "r-01", "name": "Chalet du Fjord", "category": "hebergement",
      "status": "booked", "date": "2026-08-29", "partId": 2,
      "location": "Saguenay", "bookingRef": "FJORD-77", "contactId": "ct-02", "price": 145.0 }
  ],
  "ideas": [
    { "id": "i-03", "title": "Rando au parc du Fjord", "category": "rando",
      "partId": 2, "location": "Rivière-Éternité", "status": "idea" }
  ],
  "contacts": [
    { "id": "ct-02", "name": "Chalet du Fjord", "contactPerson": "Marc",
      "phone": "+1 418-555-1234", "address": "Saguenay", "links": [] }
  ]
}
```

- Réservation `category` : `hebergement | resto | activite | transport | autre` ;
  `status` : `tobook | booked | paid`.
- Idée `category` : `rando | visite | resto | detente | shopping | autre` ;
  `status` : `idea | planned | done` ; `partId` = l'étape où elle est pertinente.

## Développement

```bash
dotnet run --project RoadTripQC
```
