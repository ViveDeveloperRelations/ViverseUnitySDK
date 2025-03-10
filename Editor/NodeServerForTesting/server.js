const path = require('path');
const fs = require('fs');

function requireFromLib(moduleName) {
    return require(require.resolve(moduleName, { paths: [path.join(__dirname, 'libs/lib/node_modules')] }));
}

const express = requireFromLib('express');
const https = requireFromLib('https');
const morgan = requireFromLib('morgan');
const cors = requireFromLib('cors');

const certFile = process.argv[2];
const keyFile = process.argv[3];

// Resolve the static content directory (one level up + "Build")
const staticDir = path.resolve(__dirname, '..', 'Build');

if (!fs.existsSync(certFile) || !fs.existsSync(keyFile)) {
    console.error('Error: SSL certificate files not found.');
    process.exit(1);
}

const options = {
    key: fs.readFileSync(keyFile),
    cert: fs.readFileSync(certFile)
};

const app = express();

// Enable detailed request logging
app.use(morgan('combined'));

// Allowed origins
const allowedOrigins = [
    'https://create.viverse.com',
    'https://www.viverse.com'
];

// CORS Middleware (MUST come before app.options)
app.use(cors({
    origin: function (origin, callback) {
        if (!origin || allowedOrigins.includes(origin)) {
            callback(null, true);
        } else {
            callback(new Error('CORS policy does not allow this origin'));
        }
    },
    methods: ['GET', 'HEAD', 'PUT', 'PATCH', 'POST', 'DELETE', 'OPTIONS'],
    allowedHeaders: ['Content-Type', 'Authorization', 'AccessToken', 'accesstoken', 'Origin', 'Accept'],
    exposedHeaders: ['AccessToken', 'accesstoken'],
    credentials: true
}));

// Ensure OPTIONS Preflight is Handled Correctly
app.options('*', (req, res) => {
    const origin = req.get('Origin');
    res.header('Access-Control-Allow-Origin', origin && allowedOrigins.includes(origin) ? origin : 'https://create.viverse.com');
    res.header('Access-Control-Allow-Methods', 'GET,HEAD,PUT,PATCH,POST,DELETE,OPTIONS');
    res.header('Access-Control-Allow-Headers', 'Content-Type, Authorization, AccessToken, accesstoken, Origin, Accept');
    res.header('Access-Control-Expose-Headers', 'AccessToken, accesstoken');
    res.header('Access-Control-Allow-Credentials', 'true');
    res.status(204).end();
});



// Serve static files
app.use(express.static(staticDir, {
    extensions: ['html'], // Auto-loads index.html if directory is accessed
    setHeaders: (res, filePath) => {
        res.setHeader('Cache-Control', 'public, max-age=0');
    }
}));

// Ensure `/` loads `index.html`
app.get('/', (req, res) => {
    res.sendFile(path.join(staticDir, 'index.html'));
});

// Create HTTPS server
const server = https.createServer(options, app);
server.listen(443, () => {
    console.log(`HTTPS Server running on https://localhost:443`);
    console.log(`Serving files from: ${staticDir}`);
});