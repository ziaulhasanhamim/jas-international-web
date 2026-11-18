import { readFile, writeFile } from 'node:fs/promises';
import { watch } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { compile, optimize } from '@tailwindcss/node';
import { Scanner } from '@tailwindcss/oxide';

const root = process.cwd();
const input = resolve(root, 'JASIntWeb/wwwroot/css/input.css');
const output = resolve(root, 'JASIntWeb/wwwroot/css/output.css');
const watching = process.argv.includes('--watch');
let building = false;
let pending = false;

async function build() {
    if (building) { pending = true; return; }
    building = true;
    try {
        const compiler = await compile(await readFile(input, 'utf8'), {
            base: dirname(input), from: input, onDependency() {},
        });
        // Explicit @source folders exclude build outputs and keep CI/local generation identical.
        const sources = compiler.root === 'none' ? [] : [{ ...(compiler.root ?? { base: root, pattern: '**/*' }), negated: false }];
        const scanner = new Scanner({ sources: [...sources, ...compiler.sources] });
        const css = compiler.build(scanner.scan());
        await writeFile(output, optimize(css, { file: input, minify: !watching }).code);
        process.stdout.write('CSS generated.\n');
    } finally {
        building = false;
        if (pending) { pending = false; await build(); }
    }
}

await build();
if (watching) {
    let timer;
    const watcher = watch(root, { recursive: true }, (_event, filename) => {
        if (!filename || /(^|[/\\])(node_modules|bin|obj|\.git)([/\\]|$)/.test(filename)) return;
        if (resolve(root, filename) === output || !/\.(razor|cs|html|js|mjs|md|json|css)$/.test(filename)) return;
        clearTimeout(timer);
        timer = setTimeout(() => build().catch(error => process.stderr.write(`${error.message}\n`)), 150);
    });
    process.on('SIGINT', () => { watcher.close(); clearTimeout(timer); });
}
