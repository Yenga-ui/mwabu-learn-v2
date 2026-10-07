// A dedicated, disposable database and certificate. Never uses an application database.
import { Client } from "pg";
import { randomBytes, randomUUID } from "node:crypto";
import { spawn } from "node:child_process";
import { mkdtemp, writeFile, readFile, mkdir, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { resolve, join } from "node:path";
import https from "node:https";
import http from "node:http";

const root = resolve("..");
const source = process.env.MWABU_POSTGRES_TEST_SERVER;
if (!source)
  throw new Error(
    "Set MWABU_POSTGRES_TEST_SERVER to an isolated loopback PostgreSQL test server.",
  );
const fields = Object.fromEntries(
  source
    .split(";")
    .filter(Boolean)
    .map((part) => {
      const at = part.indexOf("=");
      return [part.slice(0, at).trim().toLowerCase(), part.slice(at + 1)];
    }),
);
if (
  !["127.0.0.1", "localhost", "::1"].includes(fields.host) ||
  fields.database !== "postgres"
)
  throw new Error(
    "Browser tests require a loopback maintenance database named postgres.",
  );
const admin = new Client({
  host: fields.host,
  port: Number(fields.port || 5432),
  database: "postgres",
  user: fields.username,
  password: fields.password,
});
const database = "mwabu_e2e_" + randomUUID().replaceAll("-", "");
const temporary = await mkdtemp(join(tmpdir(), "mwabu-browser-"));
const fixturePath = resolve(".local/e2e/fixture.json");
const secret = () => "A7!" + randomBytes(32).toString("base64");
const certificatePassword = secret();
const adminPassword = secret();
const roles = [
  "OrganisationAdmin",
  "ProjectManager",
  "HeadTeacher",
  "Teacher",
  "Learner",
  "ParentGuardian",
  "ContentManager",
  "DataAnalyst",
];
const passwords = Object.fromEntries(roles.map((role) => [role, secret()]));
if (process.env.GITHUB_ACTIONS)
  for (const value of [
    certificatePassword,
    adminPassword,
    ...Object.values(passwords),
  ])
    console.log("::add-mask::" + value);
let server;
let readiness;
let created = false;
let stopping = false;
async function run(command, args, env = process.env) {
  await new Promise((done, fail) => {
    const child = spawn(command, args, {
      cwd: root,
      env,
      windowsHide: true,
      stdio: ["ignore", "pipe", "pipe"],
    });
    let output = "";
    child.stdout.on("data", (data) => {
      output += data;
    });
    child.stderr.on("data", (data) => {
      output += data;
    });
    child.on("error", fail);
    child.on("exit", (code) =>
      code === 0
        ? done()
        : fail(
            new Error("Test setup command failed: " + command + "\n" + output),
          ),
    );
  });
}
async function cleanup() {
  if (stopping) return;
  stopping = true;
  readiness?.close();
  if (server && server.exitCode === null) {
    const exited = new Promise((done) => server.once("exit", done));
    server.kill();
    await exited;
  }
  if (created)
    await admin.query('DROP DATABASE "' + database + '" WITH (FORCE)');
  await admin.end();
  await rm(temporary, { recursive: true, force: true });
  await rm(fixturePath, { force: true });
}
process.on("SIGINT", () => void cleanup().then(() => process.exit()));
process.on("SIGTERM", () => void cleanup().then(() => process.exit()));
try {
  const pfx = join(temporary, "test.pfx");
  const pem = join(temporary, "test.crt");
  const certificateEnv = {
    ...process.env,
    MWABU_TEST_PFX: pfx,
    MWABU_TEST_PEM: pem,
    MWABU_TEST_CERT_PASSWORD: certificatePassword,
  };
  if (process.platform === "win32") {
    await run(
      "powershell.exe",
      [
        "-NoProfile",
        "-NonInteractive",
        "-Command",
        "$rsa=[Security.Cryptography.RSA]::Create(2048); $request=[Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=localhost',$rsa,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1); $san=[Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new(); $san.AddDnsName('localhost'); $san.AddIpAddress([Net.IPAddress]::Parse('127.0.0.1')); $request.CertificateExtensions.Add($san.Build()); $cert=$request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5),[DateTimeOffset]::UtcNow.AddDays(1)); [IO.File]::WriteAllBytes($env:MWABU_TEST_PFX,$cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx,$env:MWABU_TEST_CERT_PASSWORD)); $body=[Convert]::ToBase64String($cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert),[Base64FormattingOptions]::InsertLineBreaks); [IO.File]::WriteAllText($env:MWABU_TEST_PEM,\"-----BEGIN CERTIFICATE-----`n$body`n-----END CERTIFICATE-----\"); $cert.Dispose(); $rsa.Dispose();",
      ],
      certificateEnv,
    );
  } else {
    const key = join(temporary, "test.key");
    await run("openssl", [
      "req",
      "-x509",
      "-newkey",
      "rsa:2048",
      "-nodes",
      "-keyout",
      key,
      "-out",
      pem,
      "-days",
      "1",
      "-subj",
      "/CN=localhost",
      "-addext",
      "subjectAltName=DNS:localhost,IP:127.0.0.1",
    ]);
    await run(
      "openssl",
      [
        "pkcs12",
        "-export",
        "-inkey",
        key,
        "-in",
        pem,
        "-out",
        pfx,
        "-passout",
        "env:MWABU_TEST_CERT_PASSWORD",
      ],
      certificateEnv,
    );
  }
  await admin.connect();
  await admin.query('CREATE DATABASE "' + database + '"');
  created = true;
  const connection = source.replace(
    /Database=postgres/i,
    "Database=" + database,
  );
  const env = {
    ...process.env,
    MWABU_MIGRATIONS_CONNECTION: connection,
    ASPNETCORE_ENVIRONMENT: "Development",
    ASPNETCORE_URLS: "https://127.0.0.1:7443",
    ConnectionStrings__MwabuLearnDb: connection,
    Kestrel__Certificates__Default__Path: pfx,
    Kestrel__Certificates__Default__Password: certificatePassword,
    Jwt__Issuer: "https://127.0.0.1:7443",
    Jwt__Audience: "mwabu-browser-test",
    Jwt__SigningKeyBase64: randomBytes(64).toString("base64"),
    BootstrapAdministrator__Enabled: "true",
    BootstrapAdministrator__Email: "platform@mwabu.invalid",
    BootstrapAdministrator__Password: adminPassword,
    BootstrapAdministrator__FirstName: "Platform",
    BootstrapAdministrator__LastName: "Administrator",
    BootstrapAdministrator__OrganisationName: "Mwabu",
    BootstrapAdministrator__OrganisationCode: "MWABU",
    DevelopmentSeed__Enabled: "true",
    BackgroundWork__Enabled: "false",
    ContentStorage__RootPath: join(temporary, "content"),
    HttpSecurity__AllowedOrigins__0: "https://127.0.0.1:7443",
    DataProtection__PersistKeysInDatabase: "true",
    DataProtection__Certificate__Path: pfx,
    DataProtection__Certificate__Password: certificatePassword,
    Authentication__LoginAttemptsPerMinute: "100",
    ...Object.fromEntries(
      roles.map((role) => [
        "DevelopmentSeed__Passwords__" + role,
        passwords[role],
      ]),
    ),
  };
  const ef = process.env.MWABU_EF_PATH;
  await run(
    ef || "dotnet",
    [
      ...(ef ? [] : ["ef"]),
      "database",
      "update",
      "--configuration",
      "Release",
      "--no-build",
      "--project",
      "backend/src/MwabuLearn.Infrastructure",
      "--startup-project",
      "backend/src/MwabuLearn.Api",
    ],
    env,
  );
  server = spawn(
    "dotnet",
    [
      resolve(
        root,
        "backend/src/MwabuLearn.Api/bin/Release/net10.0/MwabuLearn.Api.dll",
      ),
      "--webroot",
      resolve("dist"),
    ],
    {
      cwd: resolve(root, "backend/src/MwabuLearn.Api"),
      env,
      windowsHide: true,
      stdio: ["ignore", "ignore", "pipe"],
    },
  );
  server.stderr.on("data", () => {});
  const ca = await readFile(pem);
  async function request(path, body, token) {
    return await new Promise((done, fail) => {
      const req = https.request(
        "https://127.0.0.1:7443" + path,
        {
          ca,
          method: body ? "POST" : "GET",
          headers: {
            ...(body ? { "Content-Type": "application/json" } : {}),
            ...(token ? { Authorization: "Bearer " + token } : {}),
          },
        },
        (res) => {
          let data = "";
          res.on("data", (part) => {
            data += part;
          });
          res.on("end", () =>
            res.statusCode < 300
              ? done(data ? JSON.parse(data) : null)
              : fail(
                  new Error(
                    "Test setup HTTP " + res.statusCode + " at " + path,
                  ),
                ),
          );
        },
      );
      req.on("error", fail);
      req.end(body ? JSON.stringify(body) : undefined);
    });
  }
  let ready = false;
  for (let attempt = 0; attempt < 120; attempt++) {
    if (server.exitCode !== null)
      throw new Error("Isolated test API exited before becoming ready.");
    try {
      await request("/health/live");
      ready = true;
      break;
    } catch {
      await new Promise((done) => setTimeout(done, 500));
    }
  }
  if (!ready) throw new Error("Isolated test API readiness timed out.");
  const login = await request("/api/auth/login", {
    email: "platform@mwabu.invalid",
    password: adminPassword,
  });
  const seeded = await request("/api/development/seed", {}, login.accessToken);
  await mkdir(resolve(".local/e2e"), { recursive: true });
  await writeFile(
    fixturePath,
    JSON.stringify({
      ...seeded,
      admin: { email: "platform@mwabu.invalid", password: adminPassword },
      roles: Object.fromEntries(
        roles.map((role) => [
          role,
          {
            email: "demo." + role.toLowerCase() + "@mwabu.invalid",
            password: passwords[role],
          },
        ]),
      ),
    }),
    { mode: 0o600 },
  );
  console.log(
    "Isolated HTTPS browser test API ready; demonstration accounts seeded.",
  );
  readiness = http
    .createServer(async (request, response) => {
      if (request.method === "POST" && request.url === "/shutdown") {
        await cleanup();
        response.end("stopped");
        return;
      }
      response.end("ready");
    })
    .listen(7444, "127.0.0.1");
} catch (error) {
  console.error(error.message);
  await cleanup();
  process.exitCode = 1;
}
