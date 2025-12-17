![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![Build](https://img.shields.io/badge/status-live-success)
![Auth](https://img.shields.io/badge/auth-JWT%20HttpOnly-blue)
# studenttime-api

API REST pour l'application de suivi de temps d'étude StudentTime. Cette API permet de gérer l'authentification des utilisateurs et le suivi des sessions d'étude.

## 🚀 Fonctionnalités

- **Authentification** : Inscription, connexion, authentification Google OAuth
- **Gestion des utilisateurs** : Profil utilisateur, vérification d'email, réinitialisation de mot de passe
- **Suivi de temps** : Création, modification, suppression de sessions d'étude
- **Statistiques** : Calcul des statistiques de temps d'étude
- **Documentation Swagger** : Documentation interactive de l'API

## 📋 Prérequis

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQLite (pour le développement local - inclus avec .NET)
- Visual Studio 2022 / VS Code / Rider (optionnel, mais recommandé)
- Postman (optionnel, pour tester l'API)

## 🛠️ Installation

### 1. Cloner le repository

```bash
git clone https://github.com/votre-username/studenttime-api.git
cd studenttime-api
```

### 2. Restaurer les dépendances

```bash
dotnet restore
```

### 3. Configuration

Le fichier `appsettings.Development.json` contient déjà des valeurs d'exemple pour le développement local.

**Pour tester rapidement :**
- La clé JWT de développement est déjà configurée (vous pouvez l'utiliser telle quelle pour tester)
- La base de données SQLite sera créée automatiquement

**Pour utiliser Google OAuth et Email (optionnel) :**
Éditez `StudentTIme.API/appsettings.Development.json` et remplacez :
- `YOUR_GOOGLE_CLIENT_ID_HERE` par votre Google Client ID
- `YOUR_GOOGLE_CLIENT_SECRET_HERE` par votre Google Client Secret
- `YOUR_SENDGRID_API_KEY_HERE` par votre clé API SendGrid

**Note :** Pour la production, utilisez des variables d'environnement (Railway, Azure, etc.) ou créez votre propre `appsettings.Production.json` (non commité).

### 4. Appliquer les migrations

```bash
cd StudentTIme.API
dotnet ef database update
```

Cela créera automatiquement la base de données SQLite avec toutes les tables nécessaires.

### 5. Lancer l'API

```bash
dotnet run
```

L'API sera accessible sur `http://localhost:8080` (ou le port configuré dans `launchSettings.json`).

## 📚 Documentation API

Une fois l'API lancée, la documentation Swagger est disponible à :
- **Swagger UI** : `http://localhost:8080/swagger`
- **Swagger JSON** : `http://localhost:8080/swagger/v1/swagger.json`

Swagger permet de :
- Voir tous les endpoints disponibles
- Tester les endpoints directement depuis le navigateur
- Comprendre les modèles de données
- Voir les codes de réponse possibles

## 🔐 Endpoints principaux

### Authentification

#### `POST /Auth/register`
Inscription d'un nouvel utilisateur.

**Body :**
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!",
  "displayName": "John Doe"
}
```

**Réponse (201 Created) :**
```json
{
  "userId": "guid",
  "email": "user@example.com",
  "displayName": "John Doe",
  "token": "jwt-token"
}
```

Le token JWT est également retourné dans un cookie HttpOnly.

#### `POST /Auth/login`
Connexion d'un utilisateur existant.

**Body :**
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}
```

**Réponse (200 OK) :**
```json
{
  "userId": "guid",
  "email": "user@example.com",
  "displayName": "John Doe",
  "token": "jwt-token"
}
```

#### `POST /Auth/google`
Authentification via Google OAuth.

**Body :**
```json
{
  "idToken": "google-id-token"
}
```

#### `GET /Auth/me` 🔒
Récupérer les informations de l'utilisateur actuellement connecté.

**Headers :**
```
Authorization: Bearer {token}
```

**Réponse (200 OK) :**
```json
{
  "userId": "guid",
  "email": "user@example.com",
  "displayName": "John Doe",
  "isAuthenticated": true
}
```

#### `POST /Auth/logout`
Déconnexion (supprime le cookie d'authentification).

#### `POST /Auth/forgot-password`
Demander une réinitialisation de mot de passe.

**Body :**
```json
{
  "email": "user@example.com"
}
```

#### `POST /Auth/reset-password`
Réinitialiser le mot de passe avec un code.

**Body :**
```json
{
  "email": "user@example.com",
  "code": "reset-code",
  "newPassword": "NewSecurePassword123!"
}
```

#### `POST /Auth/verify-email`
Vérifier l'email avec un code de vérification.

**Body :**
```json
{
  "email": "user@example.com",
  "code": "verification-code"
}
```

#### `POST /Auth/resend-verification`
Renvoyer le code de vérification d'email.

**Body :**
```json
{
  "email": "user@example.com"
}
```

#### `POST /Auth/validate` 🔒
Valider un token JWT.

**Headers :**
```
Authorization: Bearer {token}
```

### Sessions d'étude

Tous les endpoints ci-dessous nécessitent une authentification (header `Authorization: Bearer {token}`).

#### `POST /TimeEntries/start`
Démarrer une nouvelle session d'étude.

**Body :**
```json
{
  "subject": "Mathématiques",
  "description": "Révision chapitre 5"
}
```

**Réponse (201 Created) :**
```json
{
  "id": "guid",
  "userId": "guid",
  "subject": "Mathématiques",
  "description": "Révision chapitre 5",
  "startTime": "2024-01-15T10:00:00Z",
  "endTime": null,
  "duration": null,
  "isActive": true
}
```

#### `POST /TimeEntries`
Créer une session d'étude manuellement (avec début et fin).

**Body :**
```json
{
  "subject": "Physique",
  "description": "Exercices de mécanique",
  "startTime": "2024-01-15T10:00:00Z",
  "endTime": "2024-01-15T11:30:00Z"
}
```

#### `PUT /TimeEntries/{id}/stop`
Arrêter une session active.

**Réponse (200 OK) :**
```json
{
  "id": "guid",
  "subject": "Mathématiques",
  "startTime": "2024-01-15T10:00:00Z",
  "endTime": "2024-01-15T11:00:00Z",
  "duration": 3600,
  "isActive": false
}
```

#### `GET /TimeEntries/active`
Récupérer la session active (s'il y en a une).

**Réponse (200 OK) :**
```json
{
  "id": "guid",
  "subject": "Mathématiques",
  "startTime": "2024-01-15T10:00:00Z",
  "endTime": null,
  "isActive": true
}
```

**Réponse (204 No Content)** si aucune session active.

#### `GET /TimeEntries`
Lister toutes les sessions (avec pagination).

**Query Parameters :**
- `page` (int, default: 1) : Numéro de page
- `pageSize` (int, default: 10000) : Taille de page

**Exemple :**
```
GET /TimeEntries?page=1&pageSize=50
```

**Réponse (200 OK) :**
```json
[
  {
    "id": "guid",
    "subject": "Mathématiques",
    "startTime": "2024-01-15T10:00:00Z",
    "endTime": "2024-01-15T11:00:00Z",
    "duration": 3600,
    "isActive": false
  }
]
```

#### `GET /TimeEntries/{id}`
Récupérer une session spécifique.

#### `PUT /TimeEntries/{id}`
Modifier une session.

**Body :**
```json
{
  "subject": "Mathématiques Avancées",
  "description": "Nouvelle description",
  "startTime": "2024-01-15T10:00:00Z",
  "endTime": "2024-01-15T11:30:00Z"
}
```

#### `DELETE /TimeEntries/{id}`
Supprimer une session (soft delete).

**Réponse (204 No Content)**

#### `GET /TimeEntries/stats`
Récupérer les statistiques de temps d'étude.

**Query Parameters :**
- `startDate` (DateTime, optional) : Date de début
- `endDate` (DateTime, optional) : Date de fin

**Exemple :**
```
GET /TimeEntries/stats?startDate=2024-01-01&endDate=2024-01-31
```

**Réponse (200 OK) :**
```json
{
  "totalDuration": 36000,
  "totalDurationInHours": 10.0,
  "entryCount": 15,
  "averageDuration": 2400,
  "averageDurationInHours": 0.67
}
```

### Santé de l'API

#### `GET /health`
Vérifier l'état de l'API.

**Réponse (200 OK) :**
```json
{
  "status": "healthy",
  "timestamp": "2024-01-15T10:00:00Z"
}
```

## 🔑 Authentification

L'API utilise JWT (JSON Web Tokens) pour l'authentification. 

### Méthode 1 : Cookie HttpOnly (Recommandé pour les applications web)
Après une connexion réussie (`/Auth/login` ou `/Auth/register`), le token est automatiquement stocké dans un cookie HttpOnly. Les requêtes suivantes incluront automatiquement ce cookie.

### Méthode 2 : Header Authorization (Pour les clients API)
Pour les clients qui ne supportent pas les cookies (Postman, applications mobiles, etc.), incluez le token dans le header :

```
Authorization: Bearer {votre_token}
```

**Note :** Pour recevoir le token dans le body de la réponse (au lieu du cookie uniquement), ajoutez le header :
```
X-Use-Token-Response: true
```

## 🗄️ Base de données

### Développement (SQLite)
Par défaut, l'API utilise SQLite en développement. La base de données sera créée automatiquement dans `StudentTIme.API/studenttime.db` lors de la première migration.

### Production (PostgreSQL)
Pour la production, configurez la variable d'environnement `DATABASE_URL` ou `ConnectionStrings__DefaultConnection` avec votre chaîne de connexion PostgreSQL.

**Format PostgreSQL :**
```
Host=localhost;Port=5432;Database=studenttime;Username=user;Password=password
```

**Format Railway (DATABASE_URL) :**
```
postgresql://user:password@host:port/database
```

L'API convertit automatiquement le format Railway vers le format .NET.

## 🧪 Tests avec Postman

### Configuration initiale

1. Créez une nouvelle collection "StudentTime API"
2. Créez un environnement avec les variables suivantes :
   - `base_url` : `http://localhost:8080`
   - `token` : (sera rempli automatiquement après login)

### Tests à effectuer

#### 1. Register
```
POST {{base_url}}/Auth/register
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "Test123!",
  "displayName": "Test User"
}
```

**Script Postman (Tests) :**
```javascript
if (pm.response.code === 201) {
    var jsonData = pm.response.json();
    if (jsonData.token) {
        pm.environment.set("token", jsonData.token);
    }
}
```

#### 2. Login
```
POST {{base_url}}/Auth/login
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "Test123!"
}
```

**Script Postman (Tests) :**
```javascript
if (pm.response.code === 200) {
    var jsonData = pm.response.json();
    if (jsonData.token) {
        pm.environment.set("token", jsonData.token);
    }
}
```

#### 3. Get Current User
```
GET {{base_url}}/Auth/me
Authorization: Bearer {{token}}
```

#### 4. Start Time Entry
```
POST {{base_url}}/TimeEntries/start
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "subject": "Mathématiques",
  "description": "Révision chapitre 5"
}
```

#### 5. Get Active Entry
```
GET {{base_url}}/TimeEntries/active
Authorization: Bearer {{token}}
```

#### 6. Stop Entry
```
PUT {{base_url}}/TimeEntries/{id}/stop
Authorization: Bearer {{token}}
```

#### 7. Get All Entries
```
GET {{base_url}}/TimeEntries?page=1&pageSize=10
Authorization: Bearer {{token}}
```

#### 8. Get Stats
```
GET {{base_url}}/TimeEntries/stats
Authorization: Bearer {{token}}
```

## 🐳 Docker

### Build

```bash
docker build -t studenttime-api -f StudentTIme.API/Dockerfile .
```

### Run

```bash
docker run -p 8080:8080 \
  -e Jwt__Key="votre-clé-secrète" \
  -e DATABASE_URL="votre-chaîne-de-connexion" \
  studenttime-api
```

## 🚂 Déploiement sur Railway

Cette API est conçue pour être déployée sur Railway. La configuration en production se fait via des variables d'environnement.

### Variables d'environnement requises sur Railway

Configurez ces variables dans votre projet Railway :

#### Base de données
- `DATABASE_URL` : Chaîne de connexion PostgreSQL (format Railway : `postgresql://user:password@host:port/database`)

#### JWT
- `Jwt__Key` : Clé secrète JWT (minimum 32 caractères)
- `Jwt__Issuer` : Émetteur JWT (par défaut : `StudentTimeAPI`)
- `Jwt__Audience` : Audience JWT (par défaut : `StudentTimeClient`)

#### Google OAuth (optionnel)
- `Google__ClientId` : ID client Google OAuth
- `Google__ClientSecret` : Secret client Google OAuth

#### Email (optionnel)
- `Email__SmtpHost` : Serveur SMTP (ex: `smtp.sendgrid.net`)
- `Email__SmtpPort` : Port SMTP (ex: `587`)
- `Email__SmtpUsername` : Nom d'utilisateur SMTP (ex: `apikey` pour SendGrid)
- `Email__SmtpPassword` : Mot de passe SMTP (ex: votre clé API SendGrid)
- `Email__FromEmail` : Email expéditeur
- `Email__FromName` : Nom expéditeur

#### CORS
- `CORS_ALLOWED_ORIGINS` : Origines autorisées, séparées par des virgules (ex: `https://votre-app.up.railway.app,http://localhost:5173`)

#### Port
- `PORT` : Port sur lequel l'API écoute (Railway définit automatiquement cette variable)

### Note importante

Railway utilise le format avec double underscore (`__`) pour les clés de configuration imbriquées :
- `Jwt__Key` au lieu de `Jwt:Key`
- `Email__SmtpHost` au lieu de `Email:SmtpHost`

L'API convertit automatiquement ces variables d'environnement en configuration .NET.

## 📦 Structure du projet

```
StudentTime.API/
├── Controllers/          # Contrôleurs API
│   ├── AuthController.cs
│   └── TimeEntriesController.cs
├── Middleware/          # Middleware personnalisés
│   ├── ExceptionHandlingMiddleware.cs
│   └── RequestLoggingMiddleware.cs
├── Migrations/          # Migrations Entity Framework
├── Properties/          # Configuration de lancement
├── Program.cs           # Point d'entrée
├── appsettings.json     # Configuration de base
├── appsettings.Development.json  # Configuration développement (exemple)
└── Dockerfile           # Configuration Docker

StudentTime.Core/
├── DTOs/                # Data Transfer Objects
│   ├── Auth/
│   └── TimeTracking/
├── Entities/            # Entités du domaine
│   ├── User.cs
│   ├── TimeEntry.cs
│   ├── PasswordResetCode.cs
│   └── EmailVerificationCode.cs
├── Exceptions/          # Exceptions métier
│   ├── BusinessException.cs
│   └── NotFoundException.cs
├── Interfaces/          # Interfaces des services
│   ├── IAuthService.cs
│   ├── ITimeTrackingService.cs
│   ├── IUserRepository.cs
│   └── ...
└── Services/            # Services métier
    ├── AuthService.cs
    └── TimeTrackingService.cs

StudentTime.Infrastructure/
├── Data/                # DbContext
│   └── AppDbContext.cs
├── Repositories/        # Implémentations des repositories
│   ├── UserRepository.cs
│   ├── TimeEntryRepository.cs
│   └── ...
└── Services/            # Services d'infrastructure
    └── EmailService.cs
```

## 🔧 Configuration avancée

### Variables d'environnement

L'API peut être configurée via des variables d'environnement (priorité sur `appsettings.json`) :

- `Jwt__Key` : Clé secrète JWT
- `Jwt__Issuer` : Émetteur JWT
- `Jwt__Audience` : Audience JWT
- `DATABASE_URL` : Chaîne de connexion à la base de données
- `ConnectionStrings__DefaultConnection` : Alternative à DATABASE_URL
- `CORS_ALLOWED_ORIGINS` : Origines CORS autorisées (séparées par des virgules)
- `Email__SmtpHost` : Serveur SMTP
- `Email__SmtpPort` : Port SMTP
- `Email__SmtpUsername` : Nom d'utilisateur SMTP
- `Email__SmtpPassword` : Mot de passe SMTP
- `Email__FromEmail` : Email expéditeur
- `Email__FromName` : Nom expéditeur
- `Google__ClientId` : ID client Google OAuth
- `Google__ClientSecret` : Secret client Google OAuth

### CORS

Par défaut, l'API autorise `http://localhost:5173`. Pour modifier cela :

1. **Via appsettings.json :**
```json
{
  "Cors": {
    "AllowedOrigins": ["http://localhost:5173", "https://votre-domaine.com"]
  }
}
```

2. **Via variable d'environnement :**
```
CORS_ALLOWED_ORIGINS=http://localhost:5173 ou https://votre-domaine.com
```

### Logging

Les logs sont configurés avec Serilog :
- Console : Tous les logs
- Fichier : Seulement les warnings et erreurs (dans `logs/app-YYYYMMDD.txt`)
- Rétention : 7 jours

## 🛠️ Développement

### Ajouter une migration

```bash
cd StudentTIme.API
dotnet ef migrations add NomDeLaMigration
```

### Appliquer les migrations

```bash
dotnet ef database update
```

### Créer un script SQL de migration

```bash
dotnet ef migrations script -o Scripts/migration.sql
```


## 🤝 Contribution

Les contributions sont les bienvenues ! N'hésitez pas à :
1. Fork le projet
2. Créer une branche pour votre fonctionnalité (`git checkout -b feature/AmazingFeature`)
3. Commit vos changements (`git commit -m 'Add some AmazingFeature'`)
4. Push vers la branche (`git push origin feature/AmazingFeature`)
5. Ouvrir une Pull Request

## 📧 Contact

Héros Credo Aboto
- Email : hcredoaboto@gmail.com
- LinkedIn : https://www.linkedin.com/in/héros-credo-aboto-948410293?utm_source=share_via&utm_content=profile&utm_medium=member_ios
- Instagram : https://www.instagram.com/herosdelafoi/


## 🙏 Remerciements

- [.NET](https://dotnet.microsoft.com/)
- [Entity Framework Core](https://docs.microsoft.com/ef/core/)
- [Serilog](https://serilog.net/)
- [Swagger/OpenAPI](https://swagger.io/)


