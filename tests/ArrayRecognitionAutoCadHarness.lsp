(vl-load-com)

(defun issue17-layer (name)
  (if (not (tblsearch "LAYER" name))
    (entmakex
      (list '(0 . "LAYER") '(100 . "AcDbSymbolTableRecord")
        '(100 . "AcDbLayerTableRecord") (cons 2 name)
        '(70 . 0) '(62 . 7) '(6 . "Continuous")))))

(defun issue17-rect (cx cy layer / hx hy entity)
  (setq hx 1191.0 hy 567.0)
  (setq entity
    (entmakex
      (list '(0 . "LWPOLYLINE") '(100 . "AcDbEntity")
        (cons 8 layer) '(100 . "AcDbPolyline") '(90 . 4) '(70 . 1)
        (cons 10 (list (- cx hx) (- cy hy)))
        (cons 10 (list (+ cx hx) (- cy hy)))
        (cons 10 (list (+ cx hx) (+ cy hy)))
        (cons 10 (list (- cx hx) (+ cy hy))))))
  entity)

(defun issue17-block-definition (/ result)
  (if (not (tblsearch "BLOCK" "ISSUE17_PV_BLOCK"))
    (progn
      (entmake
        (list '(0 . "BLOCK") '(100 . "AcDbEntity") '(8 . "0")
          '(100 . "AcDbBlockBegin") '(2 . "ISSUE17_PV_BLOCK")
          '(70 . 0) '(10 0.0 0.0 0.0) '(3 . "ISSUE17_PV_BLOCK") '(1 . "")))
      (issue17-rect 0.0 0.0 "0")
      (entmake '((0 . "ENDBLK") (100 . "AcDbEntity") (8 . "0")
        (100 . "AcDbBlockEnd"))))))

(defun issue17-insert (x y angle)
  (entmakex
    (list '(0 . "INSERT") '(100 . "AcDbEntity") '(8 . "PV_TEST")
      '(100 . "AcDbBlockReference") '(2 . "ISSUE17_PV_BLOCK")
      (cons 10 (list x y 0.0)) '(41 . 1.0) '(42 . 1.0) '(43 . 1.0)
      (cons 50 angle))))

(defun issue17-count (filter / set)
  (setq set (ssget "_X" filter))
  (if set (sslength set) 0))

(defun issue17-count-tables ()
  (issue17-count '((0 . "ACAD_TABLE"))))

(defun issue17-count-ballast ()
  (issue17-count '((0 . "INSERT") (-3 ("BALLAST_TOOL")))))

(defun issue17-prepare (/ angle vx vy)
  (setvar "CMDECHO" 0)
  (setvar "FILEDIA" 0)
  (setvar "SECURELOAD" 0)
  (command "_.ERASE" "_ALL" "")
  (issue17-layer "PV_TEST")
  (setq *issue17-sample* (issue17-rect 0.0 0.0 "PV_TEST"))
  (issue17-rect 2392.0 0.0 "PV_TEST")
  (issue17-rect 0.0 1144.0 "PV_TEST")
  (issue17-rect 2392.0 1144.0 "PV_TEST")
  (issue17-block-definition)
  (setq angle (/ pi 6.0))
  (setq vx (* -1144.0 (sin angle)))
  (setq vy (* 1144.0 (cos angle)))
  (issue17-insert 9000.0 0.0 angle)
  (issue17-insert (+ 9000.0 vx) vy angle)
  (entmakex '((0 . "CIRCLE") (100 . "AcDbEntity") (8 . "PV_TEST")
    (100 . "AcDbCircle") (10 6000.0 2500.0 0.0) (40 . 250.0)))
  (command "_.REGEN")
  (command "_.ZOOM" "_E")
  (setq *issue17-window-count*
    (issue17-count '((8 . "PV_TEST"))))
  (princ (strcat "\nISSUE17_WINDOW_OBJECTS="
    (itoa *issue17-window-count*)))
  (princ "\nISSUE17_PREPARED=PASS"))

(defun issue17-run (/ t0 t1 t2 b0 b1 b2 frames)
  (setq t0 (issue17-count-tables))
  (setq *issue17-cancel-before*
    (issue17-count '((8 . "PV_TEST"))))
  (command "STPVARRAYRECOGNIZE" 20.0 nil)
  (setq *issue17-cancel-after*
    (issue17-count '((8 . "PV_TEST"))))
  (princ (strcat "\nISSUE17_CANCEL_OBJECTS="
    (itoa *issue17-cancel-before*) ","
    (itoa *issue17-cancel-after*)))
  (command "STPVARRAYRECOGNIZE" 20.0
    '(-5000.0 -5000.0 0.0) '(5000.0 5000.0 0.0) "C"
    '(-5000.0 -5000.0 0.0) '(15000.0 8000.0 0.0) "F")
  (command "STPVARRAYTABLE" '(14000.0 3000.0 0.0))
  (setq t1 (issue17-count-tables))
  (command "_.UNDO" "1")
  (setq t2 (issue17-count-tables))
  (princ (strcat "\nISSUE17_INDEPENDENT_TABLE="
    (itoa t0) "," (itoa t1) "," (itoa t2)))
  (command "STPVARRAYTABLE" '(14000.0 3000.0 0.0))

  (command "STPVBALLASTDETECT" *issue17-sample*
    '(-5000.0 -5000.0 0.0) '(5000.0 5000.0 0.0) "C"
    '(-5000.0 -5000.0 0.0) '(15000.0 8000.0 0.0) "F")
  (setq b0 (issue17-count-tables))
  (command "STPVBALLASTARRAYTABLE" '(14000.0 -3000.0 0.0))
  (setq b1 (issue17-count-tables))
  (command "_.UNDO" "1")
  (setq b2 (issue17-count-tables))
  (princ (strcat "\nISSUE17_BALLAST_TABLE="
    (itoa b0) "," (itoa b1) "," (itoa b2)))
  (command "STPVBALLASTARRAYTABLE" '(14000.0 -3000.0 0.0))
  (command "STPVBALLASTGENERATE")
  (setq frames (issue17-count-ballast))
  (princ (strcat "\nISSUE17_BALLAST_FRAMES=" (itoa frames)))
  (command "_.QSAVE")
  (princ "\nISSUE17_RUN=PASS")
  (princ))

(defun issue17-reopen ()
  (princ (strcat "\nISSUE17_REOPEN_TABLES="
    (itoa (issue17-count-tables))))
  (princ (strcat "\nISSUE17_REOPEN_BALLAST_FRAMES="
    (itoa (issue17-count-ballast))))
  (princ "\nISSUE17_REOPEN=PASS")
  (princ))

(princ)
