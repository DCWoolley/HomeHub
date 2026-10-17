// Isolated local host: fresh unique LocalDB database, migrations, random TCP port, real browser.
// No production configuration or secrets are read. Cleanup only the generated database.
const { spawn, spawnSync } = require('node:child_process');
const { createServer } = require('node:net');
const { randomUUID } = require('node:crypto');
const path = require('node:path');
const verify = require('./Test-HouseholdItemsBrowser.cjs');
(async () => {
    const root = path.resolve(__dirname, '..');
    const published = path.join(root, '.scratch', 'browser-host');
    const publish = spawnSync('dotnet', ['publish', 'src/HomeHub.Web', '--no-restore', '-o', published, '-v:q'], { cwd: root, encoding: 'utf8', windowsHide: true });
    if (publish.status !== 0) throw Error('Browser host publish failed: ' + publish.stdout + publish.stderr);
    const database = 'HomeHub_BrowserTest_' + randomUUID().replaceAll('-', '');
    const server = createServer();
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    const port = server.address().port;
    await new Promise(resolve => server.close(resolve));
    const baseUri = `http://127.0.0.1:${port}`;
    const env = { ...process.env, ASPNETCORE_ENVIRONMENT: 'Production', ASPNETCORE_URLS: baseUri,
        ConnectionStrings__DefaultConnection: `Server=(localdb)\\MSSQLLocalDB;Database=${database};Trusted_Connection=True;MultipleActiveResultSets=True`,
        Database__ApplyMigrations: 'true', Deployment__UseHttpsRedirection: 'false', Logging__LogLevel__Default: 'Warning' };
    const child = spawn('dotnet', [path.join(published, 'HomeHub.Web.dll')], { cwd: published, env, stdio: ['ignore', 'pipe', 'pipe'], windowsHide: true });
    let logs = ''; child.stdout.on('data', d => logs += d); child.stderr.on('data', d => logs += d);
    try {
        let healthy = false;
        for (let i = 0; i < 120; i++) {
            if (child.exitCode !== null) throw Error('Isolated host exited: ' + logs);
            try { healthy = (await fetch(baseUri + '/health/ready')).ok; } catch {}
            if (healthy) break;
            await new Promise(r => setTimeout(r, 500));
        }
        if (!healthy) throw Error('Isolated host did not start: ' + logs);
        try { await verify(baseUri); } catch (error) { console.error(logs); throw error; }
    } finally {
        spawnSync('taskkill', ['/pid', String(child.pid), '/T', '/F'], { windowsHide: true, stdio: 'ignore' });
        // The generated identifier is alphanumeric and cannot reference a pre-existing app database.
        const result = spawnSync('sqlcmd', ['-S', '(localdb)\\MSSQLLocalDB', '-E', '-b', '-Q', `IF DB_ID(N'${database}') IS NOT NULL BEGIN ALTER DATABASE [${database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [${database}]; END`], { windowsHide: true, encoding: 'utf8' });
        if (result.status !== 0) { console.error('Test database cleanup failed: ' + result.stderr); process.exitCode = 1; }
    }
})().catch(e => { console.error(e.message); process.exitCode = 1; });
