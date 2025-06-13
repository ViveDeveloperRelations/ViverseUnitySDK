const path = require('path');
const fs = require('fs');

function requireFromLib(moduleName) {
    //TODO: Consider adding error handling here if the module might not be found
    try {
        return require(require.resolve(moduleName, { paths: [path.join(__dirname, 'libs/lib/node_modules')] }));
    } catch (e) {
        console.error(`Failed to require module: ${moduleName}`, e);
        process.exit(1);
    }
}

const express = requireFromLib('express');
const https = requireFromLib('https');
const morgan = requireFromLib('morgan');
const cors = requireFromLib('cors');
const expressStaticGzip = requireFromLib('express-static-gzip');

// --- Argument Handling ---
if (process.argv.length < 4) {
    console.error('Usage: node your_script.js <cert_file_path> <key_file_path>');
    process.exit(1);
}
const certFile = process.argv[2];
const keyFile = process.argv[3];
const PORT = 443; // Define port consistently

// Resolve the static content directory (one level up + "Build")
const staticDir = path.resolve(__dirname, '..', 'Build');
console.log(`Attempting to serve files from: ${staticDir}`);

if (!fs.existsSync(staticDir)) {
    console.error(`Error: Static directory not found: ${staticDir}`);
    process.exit(1);
}
if (!fs.existsSync(certFile) || !fs.existsSync(keyFile)) {
    console.error(`Error: SSL certificate or key file not found.`);
    console.error(`Cert Path: ${certFile} (Exists: ${fs.existsSync(certFile)})`);
    console.error(`Key Path: ${keyFile} (Exists: ${fs.existsSync(keyFile)})`);
    process.exit(1);
}

const options = {
    key: fs.readFileSync(keyFile),
    cert: fs.readFileSync(certFile)
};

const app = express();

// --- Define MIME Types ---
// Ensure Express knows the correct types for Unity files *before* setting up static serving.
express.static.mime.define({
    'application/javascript': ['js', 'framework.js'], // Be explicit for .framework.js
    'application/octet-stream': ['data'],
    'application/wasm': ['wasm']
    // Add any other custom types needed
}, true); // The 'true' forces override if types were already defined


// --- Middleware ---

// Enable detailed request logging (Good for debugging)
app.use(morgan('combined'));

// Allowed origins
const allowedOrigins = [
    'https://create.viverse.com',
    'https://www.viverse.com',
    // Add local development URLs
    'https://localhost',
    'https://localhost:443',
    'https://127.0.0.1',
    'https://127.0.0.1:443'
];

// CORS Middleware
app.use(cors({
    origin: function (origin, callback) {
        // Allow requests with no origin (like curl, mobile apps, server-to-server)
        // OR origins in the allowed list
        if (!origin || allowedOrigins.includes(origin)) {
            callback(null, true);
        } else {
            console.warn(`CORS: Blocked origin: ${origin}`); // Log blocked origins
            callback(new Error('CORS policy does not allow this origin'));
        }
    },
    methods: ['GET', 'HEAD', 'PUT', 'PATCH', 'POST', 'DELETE', 'OPTIONS'],
    allowedHeaders: ['Content-Type', 'Authorization', 'AccessToken', 'accesstoken', 'Origin', 'Accept', 'x-htc-op-token'],
    exposedHeaders: ['AccessToken', 'accesstoken'],
    credentials: true
}));

// Handle OPTIONS Preflight requests explicitly
app.options('*', cors({
    origin: function (origin, callback) {
        if (!origin || allowedOrigins.includes(origin)) {
            callback(null, true);
        } else {
            callback(new Error('CORS policy does not allow this origin for OPTIONS'));
        }
    },
    methods: ['GET', 'HEAD', 'PUT', 'PATCH', 'POST', 'DELETE', 'OPTIONS'],
    allowedHeaders: ['Content-Type', 'Authorization', 'AccessToken', 'accesstoken', 'Origin', 'Accept', 'x-htc-op-token'],
    exposedHeaders: ['AccessToken', 'accesstoken'],
    credentials: true
}));

// --- Fix: Custom middleware to handle all compressed files (.br and .gz) before expressStaticGzip ---
app.use((req, res, next) => {
    // Check if the request is for a compressed file
    const isBrFile = req.path.endsWith('.br');
    const isGzFile = req.path.endsWith('.gz');

    next();
});

// --- Custom route to serve iframe.html by default ---
// Handle any folder path that ends with '/' (directory requests)
app.get('*/', (req, res, next) => {
    // Get the requested folder path relative to staticDir
    const requestedPath = req.path;
    const folderPath = path.join(staticDir, requestedPath);

    const iframePath = path.join(folderPath, 'iframe.html');
    const indexPath = path.join(folderPath, 'index.html');

    console.log(`🏠 Directory request for ${requestedPath} - checking for default pages:`);
    console.log(` iframe.html: ${fs.existsSync(iframePath) ? '✅' : '❌'} (${iframePath})`);
    console.log(` index.html: ${fs.existsSync(indexPath) ? '✅' : '❌'} (${indexPath})`);

    // Check for iframe.html first, then index.html
    if (fs.existsSync(iframePath)) {
        console.log('📄 Serving iframe.html as default page');
        res.sendFile(iframePath);
    } else if (fs.existsSync(indexPath)) {
        console.log('📄 Serving index.html as default page (no iframe.html found)');
        res.sendFile(indexPath);
    } else {
        console.log('❌ No default page found, continuing to static handler');
        next(); // Let expressStaticGzip handle it
    }
});

// --- Serve static files using expressStaticGzip ---
// Note: Our custom middleware will handle compressed files that need special processing
app.use('/', expressStaticGzip(staticDir, {
    enableBrotli: true,
    enableGzip: true,
    orderPreference: ['br', 'gz'], // Prefer Brotli, then gzip
    customCompressions: [
        {
            encodingName: 'br',
            fileExtension: 'br'
        },
        {
            encodingName: 'gzip',
            fileExtension: 'gz'
        }
    ],
    serveStatic: {
        maxAge: 0, // No caching
        setHeaders: (res, filePath) => {
            // More aggressive cache prevention
            res.setHeader('Cache-Control', 'no-cache, no-store, must-revalidate');

            // For debugging
            const currentContentType = res.getHeader('Content-Type');
            const isCompressed = filePath.endsWith('.br') || filePath.endsWith('.gz');
            console.log(`express-static-gzip serving: ${path.basename(filePath)}, Type: ${currentContentType}, Compressed: ${isCompressed}`);

            // Set appropriate content types for Unity WebGL files
            if (filePath.endsWith('.js') || filePath.includes('.framework.js')) {
                res.setHeader('Content-Type', 'application/javascript');
                console.log('Setting Content-Type: application/javascript for js file');
            }
            else if (filePath.endsWith('.data') || filePath.includes('.data')) {
                res.setHeader('Content-Type', 'application/octet-stream');
                console.log('Setting Content-Type: application/octet-stream for data file');
            }
            else if (filePath.endsWith('.wasm') || filePath.includes('.wasm')) {
                res.setHeader('Content-Type', 'application/wasm');
                console.log('Setting Content-Type: application/wasm for wasm file');
            }

            // Set proper Content-Encoding headers for compressed files
            if (filePath.endsWith('.br')) {
                res.setHeader('Content-Encoding', 'br');
                console.log('Setting Content-Encoding: br for Brotli compressed file');

                // Also set the correct content type for the underlying file
                if (filePath.endsWith('.js.br') || filePath.endsWith('.framework.js.br')) {
                    res.setHeader('Content-Type', 'application/javascript');
                }
                else if (filePath.endsWith('.data.br')) {
                    res.setHeader('Content-Type', 'application/octet-stream');
                }
                else if (filePath.endsWith('.wasm.br')) {
                    res.setHeader('Content-Type', 'application/wasm');
                }
            }
            else if (filePath.endsWith('.gz')) {
                res.setHeader('Content-Encoding', 'gzip');
                console.log('Setting Content-Encoding: gzip for Gzip compressed file');

                // Also set the correct content type for the underlying file
                if (filePath.endsWith('.js.gz') || filePath.endsWith('.framework.js.gz')) {
                    res.setHeader('Content-Type', 'application/javascript');
                }
                else if (filePath.endsWith('.data.gz')) {
                    res.setHeader('Content-Type', 'application/octet-stream');
                }
                else if (filePath.endsWith('.wasm.gz')) {
                    res.setHeader('Content-Type', 'application/wasm');
                }
            }
        }
    }
}));


// --- Create HTTPS server ---
const server = https.createServer(options, app);

server.listen(PORT, () => {
    console.log(`HTTPS Server running on https://localhost:${PORT}`);
    console.log(`Serving files from: ${staticDir}`);
    console.log(`Allowed CORS origins: ${allowedOrigins.join(', ')}`);
});

// --- Handle server startup errors ---
server.on('error', (error) => {
    if (error.syscall !== 'listen') {
        throw error;
    }
    switch (error.code) {
        case 'EACCES':
            console.error(`Port ${PORT} requires elevated privileges (e.g., run with sudo)`);
            process.exit(1);
            break;
        case 'EADDRINUSE':
            console.error(`Port ${PORT} is already in use.`);
            process.exit(1);
            break;
        default:
            throw error;
    }
});

// --- Graceful Shutdown ---
process.on('SIGTERM', () => {
    console.log('SIGTERM signal received: closing HTTPS server');
    server.close(() => {
        console.log('HTTPS server closed');
        process.exit(0);
    });
});

process.on('SIGINT', () => {
    console.log('SIGINT signal received: closing HTTPS server');
    server.close(() => {
        console.log('HTTPS server closed');
        process.exit(0);
    });
});