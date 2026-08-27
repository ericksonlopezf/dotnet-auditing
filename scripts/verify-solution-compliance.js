// Copyright © Erickson Lopez. MIT License.
const fs = require('fs');
const path = require('path');

const rootDir = path.resolve(__dirname, '..');
let errors = [];

console.log('====================================================');
console.log(' EricksonLopez.Auditing Solution Compliance Verifier');
console.log('====================================================');

// 1. Verify Markdown Naming Convention (kebab-case)
const standardExceptions = new Set([
  'README.md',
  'LICENSE',
  'LICENSE.md',
  'SECURITY.md',
  'CONTRIBUTING.md',
  'CODE_OF_CONDUCT.md',
  'CHANGELOG.md',
  'SUPPORT.md',
  'GOVERNANCE.md',
  'PULL_REQUEST_TEMPLATE.md'
]);

function getAllFiles(dir, filter) {
  let results = [];
  const list = fs.readdirSync(dir);
  for (const item of list) {
    if (item === 'bin' || item === 'obj' || item === 'node_modules' || item === '.git' || item === '.vs' || item === 'TestResults' || item === 'StrykerOutput' || item === 'results' || item === 'BenchmarkDotNet.Artifacts' || item.startsWith('coveragereport')) {
      continue;
    }
    const fullPath = path.join(dir, item);
    const stat = fs.statSync(fullPath);
    if (stat.isDirectory()) {
      results = results.concat(getAllFiles(fullPath, filter));
    } else if (!filter || filter(fullPath)) {
      results.push(fullPath);
    }
  }
  return results;
}

const mdFiles = getAllFiles(rootDir, p => p.endsWith('.md'));
for (const mdFile of mdFiles) {
  const basename = path.basename(mdFile);
  if (standardExceptions.has(basename)) continue;
  const nameWithoutExt = basename.replace(/\.md$/, '');
  const isKebabCase = /^[a-z0-9]+(-[a-z0-9]+)*$/.test(nameWithoutExt);
  if (!isKebabCase) {
    errors.push(`Markdown naming violation: "${mdFile}" is not kebab-case.`);
  }
}

// 2. Verify CS Copyright Header and Forbidden Pragmas
const csFiles = getAllFiles(rootDir, p => p.endsWith('.cs'));
const expectedHeader = '// Copyright © Erickson Lopez. MIT License.';

for (const csFile of csFiles) {
  const content = fs.readFileSync(csFile, 'utf8');
  if (!content.startsWith(expectedHeader)) {
    errors.push(`Missing or incorrect copyright header in "${csFile}". Expected: "${expectedHeader}"`);
  }

  if (content.includes('#pragma warning disable CS0618') || content.includes('#pragma warning disable CS0619')) {
    errors.push(`Forbidden CS0618/CS0619 pragma warning suppression in "${csFile}".`);
  }

  if (content.includes('[Obsolete') || content.includes('[System.Obsolete')) {
    errors.push(`Forbidden [Obsolete] attribute in active code: "${csFile}".`);
  }
}

// 3. Verify .csproj Files (No local ImplicitUsings, No forbidden NoWarn)
const csprojFiles = getAllFiles(rootDir, p => p.endsWith('.csproj'));
for (const csproj of csprojFiles) {
  const content = fs.readFileSync(csproj, 'utf8');
  if (content.includes('<ImplicitUsings>')) {
    errors.push(`Redundant/overriding <ImplicitUsings> in "${csproj}". It must be centralized in Directory.Build.props.`);
  }
  if (content.includes('CS1591') || content.includes('CA1707') || content.includes('CA1852') || content.includes('CA1822') || content.includes('CA1515')) {
    errors.push(`Forbidden <NoWarn> suppression for analyzer rules in "${csproj}".`);
  }
}

// 4. Verify Contact Email in Documentation
const docsToCheck = ['CONTRIBUTING.md', 'CODE_OF_CONDUCT.md', 'SUPPORT.md', 'SECURITY.md', 'GOVERNANCE.md'];
for (const doc of docsToCheck) {
  const docPath = path.join(rootDir, doc);
  if (fs.existsSync(docPath)) {
    const content = fs.readFileSync(docPath, 'utf8');
    if (content.includes('ericksonlopez.dev@gmail.com')) {
      errors.push(`Outdated email "ericksonlopez.dev@gmail.com" found in "${doc}". Must be "ericksonlopezf@gmail.com".`);
    }
  }
}

// 5. Verify One Top-Level Type Per File in src/
const srcCsFiles = getAllFiles(path.join(rootDir, 'src'), p => p.endsWith('.cs'));
for (const csFile of srcCsFiles) {
  const content = fs.readFileSync(csFile, 'utf8');
  const lines = content.split('\n');
  let topLevelTypeCount = 0;
  let inBlockComment = false;

  for (const line of lines) {
    const trimmed = line.trim();
    if (trimmed.startsWith('/*')) inBlockComment = true;
    if (inBlockComment) {
      if (trimmed.includes('*/')) inBlockComment = false;
      continue;
    }
    if (trimmed.startsWith('//') || trimmed.startsWith('#')) continue;

    // Check for non-indented (top-level) type declarations
    // Must start at column 0 with access modifier or type keyword
    if (/^(?:public|internal)?\s*(?:static\s+|sealed\s+|abstract\s+|partial\s+|readonly\s+)*(?:class|struct|interface|enum|record(?:\s+struct|\s+class)?)\s+[A-Za-z0-9_]+/m.test(line) && !line.startsWith(' ') && !line.startsWith('\t')) {
      topLevelTypeCount++;
    }
  }

  if (topLevelTypeCount > 1) {
    errors.push(`Multiple top-level types (${topLevelTypeCount}) detected in "${csFile}". Enforce 1 type per file.`);
  }
}

// 6. Verify Third-Party Dependencies & Project References Compliance
const packagesPropsPath = path.join(rootDir, 'Directory.Packages.props');
if (!fs.existsSync(packagesPropsPath)) {
  errors.push('Missing "Directory.Packages.props" for Central Package Management.');
} else {
  const packagesContent = fs.readFileSync(packagesPropsPath, 'utf8');
  if (!packagesContent.includes('<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>')) {
    errors.push('"Directory.Packages.props" must enable <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>.');
  }

  const declaredPackages = new Set();
  const pkgVersionRegex = /<PackageVersion\s+Include="([^"]+)"/g;
  let match;
  while ((match = pkgVersionRegex.exec(packagesContent)) !== null) {
    declaredPackages.add(match[1].toLowerCase());
  }

  for (const csproj of csprojFiles) {
    const content = fs.readFileSync(csproj, 'utf8');
    const dirname = path.dirname(csproj);

    // Rule 6.1: No direct binary/DLL assembly references (<Reference Include="..."> or <HintPath>)
    if (/<Reference\s+Include=/i.test(content) || /<HintPath>/i.test(content)) {
      errors.push(`Forbidden direct binary <Reference> or <HintPath> in "${csproj}". All third-party dependencies must be NuGet packages (<PackageReference>).`);
    }

    // Rule 6.2: No local package versions (must use Central Package Management)
    const pkgRefWithVersionRegex = /<PackageReference\s+[^>]*\bVersion\s*=\s*"[^"]*"/gi;
    if (pkgRefWithVersionRegex.test(content)) {
      errors.push(`Forbidden inline "Version" attribute in <PackageReference> in "${csproj}". Manage all package versions centrally in "Directory.Packages.props".`);
    }

    // Rule 6.3: All PackageReferences must be declared in Directory.Packages.props
    const pkgRefRegex = /<PackageReference\s+Include="([^"]+)"/g;
    let pkgMatch;
    while ((pkgMatch = pkgRefRegex.exec(content)) !== null) {
      const pkgName = pkgMatch[1];
      if (!declaredPackages.has(pkgName.toLowerCase())) {
        errors.push(`Package "${pkgName}" in "${csproj}" is not declared in "Directory.Packages.props".`);
      }
    }

    // Rule 6.4: ProjectReferences must strictly be internal workspace projects
    const projRefRegex = /<ProjectReference\s+Include="([^"]+)"/g;
    let projMatch;
    while ((projMatch = projRefRegex.exec(content)) !== null) {
      const relPath = projMatch[1].replace(/\\/g, '/');
      const resolvedPath = path.resolve(dirname, relPath);

      if (!fs.existsSync(resolvedPath)) {
        errors.push(`Invalid <ProjectReference> in "${csproj}": "${relPath}" does not exist.`);
      } else if (!resolvedPath.startsWith(rootDir)) {
        errors.push(`Forbidden external <ProjectReference> in "${csproj}": "${relPath}" points outside the repository.`);
      }
    }
  }
}

// Summary Report
if (errors.length > 0) {
  console.error('\n❌ Solution compliance verification FAILED with the following errors:');
  for (const err of errors) {
    console.error(`  - ${err}`);
  }
  process.exit(1);
} else {
  console.log('\n✅ All solution compliance checks PASSED successfully!');
  console.log(`  - Verified ${mdFiles.length} markdown documents (kebab-case compliance).`);
  console.log(`  - Verified ${csFiles.length} C# source files (headers, no obsoletes, no pragmas).`);
  console.log(`  - Verified ${csprojFiles.length} project files (no local overrides, clean warning levels).`);
  console.log(`  - Verified contact emails and maintainer identity across all governance docs.`);
  console.log(`  - Verified single-type per file invariant across all src/ packages.`);
  console.log(`  - Verified strict NuGet third-party dependency & Central Package Management (CPM) compliance.`);
  process.exit(0);
}

