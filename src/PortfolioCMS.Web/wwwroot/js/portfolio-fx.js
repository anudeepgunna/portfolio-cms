/* ─────────────────────────────────────────────────────────────────────────────
 * portfolio-fx — WebGL hero backdrop plus scroll and pointer effects.
 *
 * Everything here is progressive enhancement. Blazor renders a complete,
 * readable page on its own; if three.js fails to load, WebGL is unavailable,
 * or the visitor prefers reduced motion, the page keeps working and simply
 * loses the motion. Nothing in here is load-bearing for content.
 *
 * Exposed as window.portfolioFx so Blazor can drive it through IJSRuntime.
 * ───────────────────────────────────────────────────────────────────────────*/

const THREE_SOURCES = [
  'https://cdn.jsdelivr.net/npm/three@0.169.0/build/three.module.min.js',
  'https://unpkg.com/three@0.169.0/build/three.module.min.js',
];

const reducedMotion = () =>
  window.matchMedia('(prefers-reduced-motion: reduce)').matches;

let threeModule = null;

async function loadThree() {
  if (threeModule) return threeModule;

  for (const src of THREE_SOURCES) {
    try {
      threeModule = await import(/* webpackIgnore: true */ src);
      return threeModule;
    } catch {
      // Try the next mirror before giving up on the effect entirely.
    }
  }
  return null;
}

/* ── Colour helpers ────────────────────────────────────────────────────────*/

// Theme colours arrive as CSS hex strings. Anything unparseable falls back to
// the default palette rather than rendering black particles on a dark page.
function hexToRgb(hex, fallback) {
  const m = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec((hex ?? '').trim());
  if (!m) return fallback;
  return [parseInt(m[1], 16) / 255, parseInt(m[2], 16) / 255, parseInt(m[3], 16) / 255];
}

/* ══════════════════════════════════════════════════════════════════════════
 * WebGL hero backdrop
 * ════════════════════════════════════════════════════════════════════════*/

class HeroScene {
  constructor(canvas, THREE, palette) {
    this.canvas = canvas;
    this.THREE = THREE;
    this.running = false;
    this.visible = true;
    this.frame = 0;

    // Pointer target vs. current, so movement eases instead of snapping.
    this.pointer = { x: 0, y: 0 };
    this.eased = { x: 0, y: 0 };
    this.scroll = 0;

    const renderer = new THREE.WebGLRenderer({
      canvas,
      antialias: false,      // FXAA-free; points are soft-edged in the shader
      alpha: true,
      powerPreference: 'low-power',
    });
    renderer.setClearColor(0x000000, 0);
    // Capping DPR keeps retina laptops from shading 4x the pixels for a
    // background that is deliberately soft.
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
    this.renderer = renderer;

    this.scene = new THREE.Scene();
    this.camera = new THREE.PerspectiveCamera(60, 1, 0.1, 100);
    this.camera.position.z = 14;

    this.buildParticles(palette);
    this.buildRings(palette);

    this.resize();
    this.bind();
  }

  /* A shell of points on a Fibonacci sphere, displaced by value noise so it
   * reads as an organic cloud rather than a geometric ball. */
  buildParticles(palette) {
    const THREE = this.THREE;
    const COUNT = 3400;

    const positions = new Float32Array(COUNT * 3);
    const colors = new Float32Array(COUNT * 3);
    const scales = new Float32Array(COUNT);
    const seeds = new Float32Array(COUNT);

    const golden = Math.PI * (3 - Math.sqrt(5));

    for (let i = 0; i < COUNT; i++) {
      const y = 1 - (i / (COUNT - 1)) * 2;
      const radiusAt = Math.sqrt(Math.max(0, 1 - y * y));
      const theta = golden * i;

      // Vary the radius so points occupy a shell with depth.
      const r = 4.6 + Math.sin(i * 0.35) * 0.7 + Math.random() * 1.1;

      positions[i * 3] = Math.cos(theta) * radiusAt * r;
      positions[i * 3 + 1] = y * r * 0.78;
      positions[i * 3 + 2] = Math.sin(theta) * radiusAt * r;

      // Blend across the three theme colours by height, so the cloud carries
      // the user's palette instead of a hardcoded one.
      const t = (y + 1) / 2;
      const [a, b, c] = palette;
      const mix = t < 0.5
        ? a.map((v, k) => v + (b[k] - v) * (t * 2))
        : b.map((v, k) => v + (c[k] - v) * ((t - 0.5) * 2));

      colors[i * 3] = mix[0];
      colors[i * 3 + 1] = mix[1];
      colors[i * 3 + 2] = mix[2];

      scales[i] = 0.5 + Math.random() * 1.6;
      seeds[i] = Math.random() * Math.PI * 2;
    }

    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    geometry.setAttribute('color', new THREE.BufferAttribute(colors, 3));
    geometry.setAttribute('aScale', new THREE.BufferAttribute(scales, 1));
    geometry.setAttribute('aSeed', new THREE.BufferAttribute(seeds, 1));

    const material = new THREE.ShaderMaterial({
      transparent: true,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      uniforms: {
        uTime: { value: 0 },
        uSize: { value: 34 },
        uOpacity: { value: 0.9 },
      },
      vertexShader: `
        attribute float aScale;
        attribute float aSeed;
        uniform float uTime;
        uniform float uSize;
        varying vec3 vColor;
        varying float vTwinkle;

        void main() {
          vColor = color;

          vec3 p = position;
          // Slow breathing displacement along the normal direction.
          float wave = sin(uTime * 0.5 + aSeed) * 0.35;
          p += normalize(p) * wave;

          vec4 mv = modelViewMatrix * vec4(p, 1.0);
          gl_Position = projectionMatrix * mv;

          // Size attenuation: nearer points render larger.
          gl_PointSize = uSize * aScale * (1.0 / -mv.z);
          vTwinkle = 0.55 + 0.45 * sin(uTime * 1.4 + aSeed * 3.0);
        }
      `,
      fragmentShader: `
        uniform float uOpacity;
        varying vec3 vColor;
        varying float vTwinkle;

        void main() {
          // Soft round sprite without needing a texture.
          float d = length(gl_PointCoord - vec2(0.5));
          if (d > 0.5) discard;
          float alpha = smoothstep(0.5, 0.0, d);
          gl_FragColor = vec4(vColor, alpha * uOpacity * vTwinkle);
        }
      `,
      vertexColors: true,
    });

    this.points = new THREE.Points(geometry, material);
    this.scene.add(this.points);
  }

  /* Two wireframe rings give the cloud a sense of orientation as it turns. */
  buildRings(palette) {
    const THREE = this.THREE;
    this.rings = new THREE.Group();

    for (let i = 0; i < 2; i++) {
      // Kept inside the visible frustum so the ring reads as an ellipse
      // rather than a line running off both edges of the screen.
      const geo = new THREE.TorusGeometry(5.6 + i * 1.1, 0.011, 8, 220);
      const mat = new THREE.MeshBasicMaterial({
        color: new THREE.Color(...palette[i === 0 ? 0 : 2]),
        transparent: true,
        opacity: 0.20 - i * 0.07,
      });
      const ring = new THREE.Mesh(geo, mat);
      // Shallow tilts only. Near edge-on, a torus collapses to a hard line.
      ring.rotation.x = 1.02 + i * 0.16;
      ring.rotation.y = i * 0.35;
      this.rings.add(ring);
    }

    this.scene.add(this.rings);
  }

  bind() {
    this.onResize = () => this.resize();
    window.addEventListener('resize', this.onResize, { passive: true });

    this.onPointer = (e) => {
      const t = e.touches ? e.touches[0] : e;
      if (!t) return;
      this.pointer.x = (t.clientX / window.innerWidth) * 2 - 1;
      this.pointer.y = (t.clientY / window.innerHeight) * 2 - 1;
    };
    window.addEventListener('pointermove', this.onPointer, { passive: true });

    this.onScroll = () => {
      this.scroll = window.scrollY || 0;
    };
    window.addEventListener('scroll', this.onScroll, { passive: true });

    // Stop rendering entirely once the hero scrolls away, and when the tab is
    // hidden — a background canvas should never cost battery off-screen.
    this.observer = new IntersectionObserver(
      ([entry]) => { this.visible = entry.isIntersecting; },
      { threshold: 0 }
    );
    this.observer.observe(this.canvas);

    this.onVisibility = () => {
      this.pageVisible = document.visibilityState === 'visible';
    };
    this.pageVisible = document.visibilityState === 'visible';
    document.addEventListener('visibilitychange', this.onVisibility);
  }

  resize() {
    const rect = this.canvas.getBoundingClientRect();
    const w = Math.max(1, rect.width);
    const h = Math.max(1, rect.height);

    this.renderer.setSize(w, h, false);
    this.camera.aspect = w / h;
    this.camera.updateProjectionMatrix();
  }

  start() {
    if (this.running) return;
    this.running = true;

    // Reduced motion still gets the composition, just frozen.
    if (reducedMotion()) {
      this.points.material.uniforms.uTime.value = 1.2;
      this.renderer.render(this.scene, this.camera);
      return;
    }

    const clock = new this.THREE.Clock();

    const tick = () => {
      if (!this.running) return;
      this.frame = requestAnimationFrame(tick);

      if (!this.visible || !this.pageVisible) return;

      const t = clock.getElapsedTime();
      this.points.material.uniforms.uTime.value = t;

      // Ease toward the pointer so motion feels weighted, not twitchy.
      this.eased.x += (this.pointer.x - this.eased.x) * 0.045;
      this.eased.y += (this.pointer.y - this.eased.y) * 0.045;

      const scrollTurn = this.scroll * 0.0012;

      this.points.rotation.y = t * 0.055 + this.eased.x * 0.5 + scrollTurn;
      this.points.rotation.x = this.eased.y * 0.32 + scrollTurn * 0.35;

      this.rings.rotation.y = -t * 0.08 + this.eased.x * 0.3;
      this.rings.rotation.z = t * 0.03;

      // Drift the camera slightly with the pointer for parallax depth.
      this.camera.position.x = this.eased.x * 1.4;
      this.camera.position.y = -this.eased.y * 1.0;
      this.camera.lookAt(0, 0, 0);

      this.renderer.render(this.scene, this.camera);
    };

    tick();
  }

  dispose() {
    this.running = false;
    cancelAnimationFrame(this.frame);

    window.removeEventListener('resize', this.onResize);
    window.removeEventListener('pointermove', this.onPointer);
    window.removeEventListener('scroll', this.onScroll);
    document.removeEventListener('visibilitychange', this.onVisibility);
    this.observer?.disconnect();

    this.points?.geometry.dispose();
    this.points?.material.dispose();
    this.rings?.traverse((o) => {
      o.geometry?.dispose();
      o.material?.dispose();
    });
    this.renderer?.dispose();
  }
}

/* ══════════════════════════════════════════════════════════════════════════
 * DOM effects — reveal on scroll, pointer tilt, progress bar
 * ════════════════════════════════════════════════════════════════════════*/

const dom = {
  revealObserver: null,
  tiltNodes: [],
  progressBar: null,
  onScrollProgress: null,
};

function setupReveal() {
  dom.revealObserver?.disconnect();

  const targets = document.querySelectorAll('[data-reveal]');
  if (!targets.length) return;

  if (reducedMotion()) {
    targets.forEach((el) => el.classList.add('is-revealed'));
    return;
  }

  dom.revealObserver = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) return;

        // Stagger children so a grid cascades instead of popping as one block.
        const delay = Number(entry.target.dataset.revealDelay || 0);
        setTimeout(() => entry.target.classList.add('is-revealed'), delay);

        // Reveal is one-way; stop observing so scrolling back up is stable.
        dom.revealObserver.unobserve(entry.target);
      });
    },
    { threshold: 0.12, rootMargin: '0px 0px -8% 0px' }
  );

  targets.forEach((el) => dom.revealObserver.observe(el));
}

/* Pointer-tracked 3D tilt plus a light that follows the cursor. Skipped on
 * coarse pointers, where there is no hover state to respond to. */
function setupTilt() {
  dom.tiltNodes.forEach(({ el, enter, move, leave }) => {
    el.removeEventListener('pointerenter', enter);
    el.removeEventListener('pointermove', move);
    el.removeEventListener('pointerleave', leave);
  });
  dom.tiltNodes = [];

  if (reducedMotion() || !window.matchMedia('(hover: hover) and (pointer: fine)').matches) return;

  document.querySelectorAll('[data-tilt]').forEach((el) => {
    const strength = Number(el.dataset.tilt) || 7;

    const move = (e) => {
      const r = el.getBoundingClientRect();
      const px = (e.clientX - r.left) / r.width;
      const py = (e.clientY - r.top) / r.height;

      el.style.setProperty('--tilt-x', `${(0.5 - py) * strength}deg`);
      el.style.setProperty('--tilt-y', `${(px - 0.5) * strength}deg`);
      el.style.setProperty('--mx', `${px * 100}%`);
      el.style.setProperty('--my', `${py * 100}%`);
    };

    const enter = () => el.classList.add('is-tilting');

    const leave = () => {
      el.classList.remove('is-tilting');
      el.style.setProperty('--tilt-x', '0deg');
      el.style.setProperty('--tilt-y', '0deg');
    };

    el.addEventListener('pointerenter', enter);
    el.addEventListener('pointermove', move);
    el.addEventListener('pointerleave', leave);

    dom.tiltNodes.push({ el, enter, move, leave });
  });
}

function setupProgress() {
  if (dom.onScrollProgress) {
    window.removeEventListener('scroll', dom.onScrollProgress);
  }

  let bar = document.querySelector('.fx-progress');
  if (!bar) {
    bar = document.createElement('div');
    bar.className = 'fx-progress';
    document.body.appendChild(bar);
  }
  dom.progressBar = bar;

  let queued = false;
  dom.onScrollProgress = () => {
    if (queued) return;
    queued = true;

    requestAnimationFrame(() => {
      queued = false;
      const max = document.documentElement.scrollHeight - window.innerHeight;
      const pct = max > 0 ? (window.scrollY / max) * 100 : 0;
      bar.style.width = `${Math.min(100, Math.max(0, pct))}%`;
    });
  };

  window.addEventListener('scroll', dom.onScrollProgress, { passive: true });
  dom.onScrollProgress();
}

/* ══════════════════════════════════════════════════════════════════════════
 * Public API
 * ════════════════════════════════════════════════════════════════════════*/

let heroScene = null;

const portfolioFx = {
  /**
   * Wire up the DOM effects and, when a hero canvas is present, the WebGL
   * backdrop. Safe to call repeatedly — Blazor re-renders replace nodes, so
   * every call re-binds against whatever is currently in the document.
   */
  async init(colors) {
    setupReveal();
    setupTilt();
    setupProgress();

    const canvas = document.querySelector('[data-fx-hero]');
    if (!canvas) return;

    // A re-init after a Blazor re-render points at a detached canvas.
    if (heroScene && heroScene.canvas !== canvas) {
      heroScene.dispose();
      heroScene = null;
    }
    if (heroScene) { heroScene.resize(); return; }

    const THREE = await loadThree();
    if (!THREE) return;                       // offline or CDN blocked

    try {
      const palette = [
        hexToRgb(colors?.primary, [0.39, 0.40, 0.95]),
        hexToRgb(colors?.secondary, [0.55, 0.36, 0.96]),
        hexToRgb(colors?.accent, [0.02, 0.71, 0.83]),
      ];

      heroScene = new HeroScene(canvas, THREE, palette);
      heroScene.start();
      canvas.classList.add('is-live');
    } catch {
      // WebGL unavailable or context creation failed — leave the CSS backdrop.
      heroScene = null;
    }
  },

  /** Re-scan the DOM after content changes without rebuilding the scene. */
  refresh() {
    setupReveal();
    setupTilt();
    heroScene?.resize();
  },

  dispose() {
    heroScene?.dispose();
    heroScene = null;

    dom.revealObserver?.disconnect();
    if (dom.onScrollProgress) window.removeEventListener('scroll', dom.onScrollProgress);
    dom.progressBar?.remove();
    dom.progressBar = null;
  },
};

/* Blazor boots on its own schedule and may call in before this module has
 * evaluated. index.html installs a stub that records those calls; replay them
 * now so the effects are never silently skipped. */
const queued = window.portfolioFx?._q ?? [];
window.portfolioFx = portfolioFx;

for (const [method, args] of queued) {
  try { portfolioFx[method]?.(...args); } catch { /* best effort */ }
}

export default portfolioFx;
