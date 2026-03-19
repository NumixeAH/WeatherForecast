# WeatherForecast Service — Guide de déploiement et d'exploitation

## Vue d'ensemble

Service Windows .NET 8 qui récupère quotidiennement (15h00) les données météo de 14 emplacements
via l'API Open-Meteo et les insère dans la base SQL Server `PhM_Meteo`.

---

## Prérequis

- **Windows 10/11 ou Windows Server 2016+**
- **.NET 8 Runtime** (pas le SDK complet) : [Télécharger](https://dotnet.microsoft.com/download/dotnet/8.0)
  - Installer le **ASP.NET Core Runtime** n'est pas nécessaire, le **Desktop Runtime** ou le **.NET Runtime** suffit.
- **Accès réseau** vers le serveur SQL (`pcsupporthome.dyndns.org:30127`)
- **Accès réseau** vers `api.open-meteo.com` (HTTPS, port 443)

---

## Compilation et publication

```bash
# Depuis la racine du projet
cd src/WeatherForecast.Service

# Publier en Release (self-contained = embarque le runtime .NET)
dotnet publish -c Release -r win-x64 --self-contained -o ../../publish

# OU publier en mode "framework-dependent" (nécessite .NET Runtime installé, plus léger)
dotnet publish -c Release -o ../../publish
```

Le dossier `publish/` contiendra tout le nécessaire.

---

## Installation du service Windows

### Option 1 : Auto-installation (recommandé)

L'exécutable gère lui-même l'installation/désinstallation du service.

```cmd
:: Ouvrir une invite de commande en ADMINISTRATEUR

:: Installer
C:\chemin\publish\WeatherForecast.Service.exe --install

:: Démarrer
sc start WeatherForecastService

:: Vérifier le statut
sc query WeatherForecastService
```

Ce que fait `--install` automatiquement :
- Crée le service `WeatherForecastService` avec démarrage **automatique (différé)**
- Ajoute une description
- Configure la **reprise après crash** : 3 redémarrages automatiques après 60 secondes

### Option 2 : Installation manuelle (sc.exe)

```cmd
:: Créer le service
sc create WeatherForecastService binPath="C:\chemin\publish\WeatherForecast.Service.exe" start=delayed-auto DisplayName="Weather Forecast Service"

:: Ajouter une description
sc description WeatherForecastService "Récupère la météo via Open-Meteo et alimente PhM_Meteo"

:: Configurer la reprise après crash (3x restart après 60s)
sc failure WeatherForecastService reset=86400 actions=restart/60000/restart/60000/restart/60000

:: Démarrer
sc start WeatherForecastService
```

---

## Désinstallation

```cmd
:: Avec l'auto-désinstallation
C:\chemin\publish\WeatherForecast.Service.exe --uninstall

:: Ou manuellement
sc stop WeatherForecastService
sc delete WeatherForecastService
```

---

## Commandes utilitaires (CLI)

L'exécutable intègre des commandes de diagnostic :

| Commande | Description |
|---|---|
| `--help` | Affiche l'aide |
| `--install` | Installe le service Windows (admin requis) |
| `--uninstall` | Désinstalle le service Windows (admin requis) |
| `--test-db` | Teste la connexion à la base de données |
| `--run-now` | Exécute un fetch météo immédiatement (INSERT dans la base) |

### Exemples

```cmd
:: Tester la connexion à la base
WeatherForecast.Service.exe --test-db

:: Sortie attendue :
::   Test de connexion à la base de données...
::     Serveur: Data Source=pcsupporthome.dyndns.org,30127;...Password=****...
::     Connexion réussie !
::     Emplacements actifs: 14
::     Dernière date météo: 18/12/2024

:: Lancer un fetch immédiat (pour tester ou rattraper un jour manqué)
WeatherForecast.Service.exe --run-now
```

---

## Configuration

Le fichier `appsettings.json` contient toute la configuration :

```json
{
  "ConnectionStrings": {
    "MeteoDb": "Data Source=...;Initial Catalog=PhM_Meteo;..."
  },
  "WeatherService": {
    "ScheduleHour": 15,
    "ScheduleMinute": 0,
    "OpenMeteoBaseUrl": "https://api.open-meteo.com/v1/forecast"
  }
}
```

## Mode développement (base locale)

Pour tester sans toucher à la base de production :

### 1. Base locale de dev (SQL Server 2025, LocalDB, etc.)

**SQL Server 2025 (Developer/Standard)** n’inclut souvent **pas** LocalDB. Utilise ton instance locale (`localhost` ou `localhost\SQLEXPRESS`).

Guide détaillé : **[docs/DEV-SETUP.md](docs/DEV-SETUP.md)**.

Exemple avec instance par défaut :

```cmd
cd src/WeatherForecast.Service
sqlcmd -S "localhost" -E -i Scripts\setup-localdb.sql
```

Avec LocalDB (si installé) : `sqlcmd -S "(localdb)\MSSQLLocalDB" -E -i Scripts\setup-localdb.sql`

### 2. Lancer en mode développement

```cmd
cd src/WeatherForecast.Service
set DOTNET_ENVIRONMENT=Development
dotnet run -- --test-db
dotnet run -- --run-now
```

`appsettings.Development.json` pointe vers **PhM_Meteo_Dev** sur `localhost` (à adapter si instance nommée).

---

## Logs et supervision

### Windows Event Log

Le service écrit dans l'Event Log Windows (source : `WeatherForecastService`, journal `Application`).

Consulter via :
- **Observateur d'événements** → Journaux Windows → Application → Source: WeatherForecastService
- Ou en PowerShell :

```powershell
Get-EventLog -LogName Application -Source WeatherForecastService -Newest 20
```

### Table Tbl_Log (base de données)

Chaque exécution écrit des logs dans `Tbl_Log` avec :
- Type ` ` (espace) : informations (démarrage, fin, résumé)
- Type `R` : requête INSERT réussie
- Type `E` : erreur

```sql
-- 20 derniers logs
SELECT TOP 20 * FROM Tbl_Log ORDER BY Log_Id DESC
```

---

## Vérifications après déploiement

1. **Tester la connexion** : `WeatherForecast.Service.exe --test-db`
2. **Tester un fetch** : `WeatherForecast.Service.exe --run-now`
3. **Vérifier les données** :
   ```sql
   SELECT TOP 14 * FROM Tbl_Meteo ORDER BY Meteo_Id DESC
   ```
4. **Vérifier les logs** :
   ```sql
   SELECT TOP 20 * FROM Tbl_Log ORDER BY Log_Id DESC
   ```
5. **Installer le service** : `WeatherForecast.Service.exe --install`
6. **Démarrer** : `sc start WeatherForecastService`
7. **Vérifier le lendemain à ~15h05** que les données sont bien insérées

---

## Dépannage

| Symptôme | Cause probable | Solution |
|---|---|---|
| `--test-db` échoue | Pare-feu / réseau | Vérifier l'accès au port 30127 |
| Pas de données à 15h | Service arrêté | `sc query WeatherForecastService` |
| Données en double | Service relancé le même jour | Le service vérifie si les données du jour existent déjà |
| Erreur HTTP Open-Meteo | Quota API / réseau | Vérifier l'accès HTTPS vers `api.open-meteo.com` |
| Service ne démarre pas | .NET Runtime manquant | Installer .NET 8 Runtime ou utiliser `--self-contained` |

---

## Mise à jour

1. Arrêter le service : `sc stop WeatherForecastService`
2. Copier les nouveaux fichiers dans le dossier de publication
3. Redémarrer : `sc start WeatherForecastService`

*Pas besoin de désinstaller/réinstaller le service pour une mise à jour.*
