// @ts-check
// `@type` JSDoc annotations allow editor autocompletion and type checking
// (when paired with `@ts-check`).
// There are various equivalent ways to declare your Docusaurus config.
// See: https://docusaurus.io/docs/api/docusaurus-config

import { themes as prismThemes } from 'prism-react-renderer';

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'OttoSynth',
  tagline: 'A wavetable VST3 synthesizer in C# // Matrix Edition',
  favicon: 'img/favicon.svg',

  url: 'https://ottosynth.local',
  baseUrl: '/',

  organizationName: 'ottosynth',
  projectName: 'ottosynth',

  onBrokenLinks: 'warn',
  onBrokenMarkdownLinks: 'warn',

  i18n: {
    defaultLocale: 'pt-BR',
    locales: ['pt-BR'],
  },

  markdown: {
    mermaid: true,
  },

  themes: ['@docusaurus/theme-mermaid'],

  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        docs: {
          sidebarPath: './sidebars.js',
          routeBasePath: '/',
          editUrl: undefined,
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      }),
    ],
  ],

  themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
    ({
      colorMode: {
        defaultMode: 'dark',
        disableSwitch: false,
        respectPrefersColorScheme: false,
      },
      navbar: {
        title: 'OttoSynth',
        logo: {
          alt: 'OttoSynth Logo',
          src: 'img/logo.svg',
        },
        items: [
          {
            type: 'docSidebar',
            sidebarId: 'docsSidebar',
            position: 'left',
            label: 'Documentação',
          },
          {
            href: 'https://github.com/ottosynth/ottosynth',
            label: 'GitHub',
            position: 'right',
          },
        ],
      },
      footer: {
        style: 'dark',
        copyright: `Copyright © ${new Date().getFullYear()} OttoSynth — built with Docusaurus.`,
      },
      prism: {
        theme: prismThemes.vsDark,
        darkTheme: prismThemes.vsDark,
        additionalLanguages: ['csharp', 'json', 'powershell', 'bash'],
      },
      mermaid: {
        theme: { light: 'dark', dark: 'dark' },
        options: {
          themeVariables: {
            primaryColor: '#00FF41',
            primaryTextColor: '#00FF41',
            primaryBorderColor: '#00FF41',
            lineColor: '#4FA659',
            secondaryColor: '#071A0E',
            tertiaryColor: '#0A1F12',
            background: '#030605',
            mainBkg: '#071A0E',
            nodeBorder: '#00FF41',
            edgeLabelBackground: '#030605',
            clusterBkg: '#0A1F12',
            clusterBorder: '#1A4D23',
            titleColor: '#00FF41',
            fontFamily: 'Cascadia Mono, Consolas, monospace',
          },
        },
      },
    }),
};

export default config;
