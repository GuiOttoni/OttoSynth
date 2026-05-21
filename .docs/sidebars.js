// @ts-check

/** @type {import('@docusaurus/plugin-content-docs').SidebarsConfig} */
const sidebars = {
  docsSidebar: [
    {
      type: 'category',
      label: 'Visão geral',
      collapsed: false,
      items: ['VisaoGeral', 'Arquitetura'],
    },
    {
      type: 'category',
      label: 'Motor DSP',
      collapsed: false,
      items: [
        'DSP-Engine',
        'Modulation-System',
        'Effects-Rack',
        'Voice-Management',
      ],
    },
    {
      type: 'category',
      label: 'Aplicação',
      collapsed: false,
      items: ['UI-Controls', 'Preset-System', 'VST3-Plugin'],
    },
    {
      type: 'category',
      label: 'Operação',
      collapsed: false,
      items: ['Como-Manter', 'Arquivos-Por-Pasta'],
    },
    {
      type: 'category',
      label: 'Apêndices',
      collapsed: false,
      items: ['Referencias'],
    },
    {
      type: 'category',
      label: 'Evolução',
      collapsed: false,
      items: ['Evolucao'],
    },
  ],
};

export default sidebars;
