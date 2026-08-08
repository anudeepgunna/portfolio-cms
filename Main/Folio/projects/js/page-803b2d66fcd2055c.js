(self.webpackChunk_N_E = self.webpackChunk_N_E || []).push([
  [895],
  {
    8360: function (e, t, r) {
      Promise.resolve().then(r.bind(r, 4290));
    },
    8792: function (e, t, r) {
      "use strict";
      r.d(t, {
        default: function () {
          return s.a;
        },
      });
      var a = r(5250),
        s = r.n(a);
    },
    9459: function (e, t, r) {
      "use strict";
      r.r(t);
      var a = r(7437),
        s = r(2169),
        l = r(703),
        i = r(2265);
      t.default = (e) => {
        let { width: t, height: r, src: n, alt: c, className: o } = e,
          [d, u] = (0, i.useState)(!0);
        return (0, a.jsx)(l.default, {
          src: n,
          alt: c || "",
          width: t,
          height: r,
          className: (0, s.cn)(
            "object-cover group-hover:opacity-75 duration-700 ease-in-out",
            d ? "grayscale blur-2xl scale-110" : "grayscale-0 blur-0 scale-100",
            o
          ),
          onLoad: () => u(!1),
        });
      };
    },
    4290: function (e, t, r) {
      "use strict";
      r.r(t),
        r.d(t, {
          default: function () {
            return g;
          },
        });
      var a = r(7437),
        s = r(2265);
      let l = [
        {
          id: "project-1",
          name: "ExpenSave",
          description:
            "Expense tracker with graphical insights, CRUD features, and animated UI.",
          demo: "https://xpensave.netlify.app/",
          github: "https://github.com/anudeepgunna/ExpenSave",
          img: "/images/projects/project-1.jpg",
          projectTags: ["framermotion", "tailwindcss"],
        },
        {
          id: "project-2",
          name: "Coin Tracer",
          description:
            "Live cryptocurrency tracker with real-time data, charts, and responsive UI.",
          demo: "https://github.com/anudeepgunna/Coin-Tracer",
          github: "https://github.com/anudeepgunna/Coin-Tracer",
          img: "/images/projects/project-2.jpg",
          projectTags: ["reactjs", "nextjs", "tailwindcss"],
        },
      ];
      var i = r(2169),
        n = r(9459),
        c = r(8792);
      let o = ["reactjs", "nextjs", "tailwindcss", "framermotion"],
        d = (e) => {
          let {
            name: t,
            description: r,
            demo: s,
            github: l,
            img: i,
            projectTags: o,
          } = e;
          return (0, a.jsxs)("article", {
            className:
              "dark:bg-gray-900 border border-gray-100 dark:border-none shadow-lg rounded-lg overflow-hidden",
            children: [
              (0, a.jsx)("div", {
                children: (0, a.jsx)(n.default, {
                  src: i,
                  height: 1080,
                  width: 2280,
                  className: "w-full h-full",
                  alt: t,
                }),
              }),
              (0, a.jsxs)("div", {
                className: "px-5 py-6",
                children: [
                  (0, a.jsx)("h1", {
                    className: "text-xl font-bold sm:text-2xl",
                    children: t,
                  }),
                  (0, a.jsx)("p", {
                    className:
                      "line-clamp-3 w-full pt-3 text-gray-600 dark:text-gray-400",
                    children: r,
                  }),
                  (0, a.jsx)("ul", {
                    className: "flex gap-3 mt-2",
                    children: o.map((e) =>
                      (0, a.jsxs)(
                        "li",
                        { className: "text-blue-500", children: ["#", e] },
                        e
                      )
                    ),
                  }),
                  (0, a.jsxs)("div", {
                    className: "flex items-center gap-3 mt-6",
                    children: [
                      (0, a.jsx)(c.default, {
                        href: s,
                        target: "_blank",
                        className:
                          "bg-slate-800 w-fit font-semibold text-white hover:bg-slate-700 rounded-full py-2 px-4 duration-300",
                        children: "Demo",
                      }),
                      (0, a.jsx)(c.default, {
                        href: l,
                        target: "_blank",
                        className:
                          "bg-slate-800 w-fit font-semibold text-white hover:bg-slate-700 rounded-full py-2 px-4 duration-300",
                        children: "GitHub",
                      }),
                    ],
                  }),
                ],
              }),
            ],
          });
        },
        u = (e) => {
          let { tags: t, onSetTags: r, disabledTags: l } = e,
            n = (0, s.useId)();
          return (0, a.jsx)("div", {
            className: "mt-8",
            children: (0, a.jsx)("ul", {
              className: "flex flex-wrap gap-2",
              children: o.map((e) =>
                (0, a.jsx)(
                  "li",
                  {
                    onClick: () => {
                      l.includes(e) &&
                        (t.includes(e)
                          ? r(t.filter((t) => t !== e))
                          : r([...t, e]));
                    },
                    className: (0, i.cn)(
                      "py-1 px-4 rounded-full border border-purple-400 dark:hover:bg-purple-500 hover:bg-purple-300 duration-300 cursor-pointer select-none",
                      t.includes(e) ? "bg-purple-300 dark:bg-purple-500" : "",
                      l.includes(e)
                        ? ""
                        : "cursor-not-allowed border-gray-300 text-gray-300 dark:border-gray-700 dark:text-gray-700 dark:hover:bg-opacity-0 hover:bg-opacity-0"
                    ),
                    children: e,
                  },
                  "tag-".concat(e + n)
                )
              ),
            }),
          });
        },
        p = (e, t) => t.every((t) => e.includes(t));
      var g = () => {
        let [e, t] = (0, s.useState)([]),
          r = l.filter((t) => p(t.projectTags, e)),
          i = r.map((e) => e.projectTags).flat(1);
        return (0, a.jsxs)("div", {
          children: [
            (0, a.jsx)(u, { disabledTags: i, tags: e, onSetTags: t }),
            (0, a.jsx)("div", {
              className: "grid gap-6 md:grid-cols-2 mt-8",
              children: r.map((e) =>
                (0, a.jsx)(
                  d,
                  {
                    name: e.name,
                    description: e.description,
                    demo: e.demo,
                    github: e.github,
                    img: e.img,
                    projectTags: e.projectTags,
                  },
                  e.id
                )
              ),
            }),
          ],
        });
      };
    },
    2169: function (e, t, r) {
      "use strict";
      r.d(t, {
        cn: function () {
          return l;
        },
      });
      var a = r(9030),
        s = r(8740);
      function l() {
        for (var e = arguments.length, t = Array(e), r = 0; r < e; r++)
          t[r] = arguments[r];
        return (0, s.m6)((0, a.Z)(t));
      }
    },
  },
  function (e) {
    e.O(0, [250, 993, 971, 69, 744], function () {
      return e((e.s = 8360));
    }),
      (_N_E = e.O());
  },
]);
