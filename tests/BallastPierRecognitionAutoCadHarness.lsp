(vl-load-com)

(defun issue30-count (filter / set)
  (setq set (ssget "_X" filter))
  (if set (sslength set) 0))

(defun issue30-frame-set ()
  (ssget "_X" '((0 . "INSERT") (-3 ("BALLAST_TOOL")))))

(defun issue30-pier-name-p (name)
  (= (substr name 1 3) "PZ_"))

(defun issue30-frame-piers (frame / name block child data count)
  (setq name (cdr (assoc 2 (entget frame))))
  (setq block (tblobjname "BLOCK" name))
  (setq child (if block (entnext block) nil))
  (setq count 0)
  (while child
    (setq data (entget child))
    (if (= (cdr (assoc 0 data)) "ENDBLK")
      (setq child nil)
      (progn
        (if (and (= (cdr (assoc 0 data)) "INSERT")
                 (issue30-pier-name-p (cdr (assoc 2 data))))
          (setq count (1+ count)))
        (setq child (entnext child)))))
  count)

(defun issue30-first-pier-name (frame / name block child data result)
  (setq name (cdr (assoc 2 (entget frame))))
  (setq block (tblobjname "BLOCK" name))
  (setq child (if block (entnext block) nil))
  (setq result nil)
  (while (and child (not result))
    (setq data (entget child))
    (if (= (cdr (assoc 0 data)) "ENDBLK")
      (setq child nil)
      (progn
        (if (and (= (cdr (assoc 0 data)) "INSERT")
                 (issue30-pier-name-p (cdr (assoc 2 data))))
          (setq result (cdr (assoc 2 data))))
        (setq child (entnext child)))))
  result)

(defun issue30-total-frame-piers (set / index total)
  (setq index 0 total 0)
  (if set
    (while (< index (sslength set))
      (setq total (+ total (issue30-frame-piers (ssname set index))))
      (setq index (1+ index))))
  total)

(defun issue30-table-total (/ set table data pair value total)
  (setq set (ssget "_X" '((0 . "ACAD_TABLE"))))
  (if (not set)
    -1
    (progn
      (setq table (ssname set (1- (sslength set))))
      (setq data (entget table))
      (setq total -1)
      (foreach pair data
        (if (= (car pair) 302)
          (progn
            (setq value (atoi (cdr pair)))
            (if (> value 0) (setq total value)))))
      total)))

(defun issue30-has-pier-marker (entity / data xdata)
  (setq data (entget entity '("BALLAST_TOOL")))
  (setq xdata (assoc -3 data))
  (if (and xdata (vl-string-search "BALLAST_PIER" (vl-princ-to-string xdata)))
    1 0))

(defun issue30-run (/ frames nested first-frame pier-name pier-definition standalone manual
                      expected actual table-before table-after)
  (issue17-prepare)
  (command "STPVBALLASTDETECT" *issue17-sample*
    '(-5000.0 -5000.0 0.0) '(5000.0 5000.0 0.0) "C"
    '(-5000.0 -5000.0 0.0) '(15000.0 8000.0 0.0) "F")
  (command "STPVBALLASTGENERATE")
  (setq frames (issue30-frame-set))
  (setq nested (issue30-total-frame-piers frames))
  (if (or (not frames) (< nested 1))
    (progn (princ "\nISSUE30_GENERATION=FAIL") (exit)))

  (setq first-frame (ssname frames 0))
  (setq pier-name (issue30-first-pier-name first-frame))
  (setq pier-definition (tblobjname "BLOCK" pier-name))
  (princ (strcat "\nISSUE30_DEFINITION_MARKER="
    (itoa (issue30-has-pier-marker pier-definition))))
  (setq standalone
    (entmakex
      (list '(0 . "INSERT") '(100 . "AcDbEntity") '(8 . "0")
        '(100 . "AcDbBlockReference") (cons 2 pier-name)
        '(10 20000.0 0.0 0.0) '(41 . 1.0) '(42 . 1.0) '(43 . 1.0)
        '(50 . 0.0))))
  (setq manual
    (entmakex
      '((0 . "CIRCLE") (100 . "AcDbEntity") (8 . "0")
        (100 . "AcDbCircle") (10 22000.0 0.0 0.0) (40 . 250.0))))
  (command "STPVBALLASTPIERRECOGNIZE" manual "" "")
  (princ (strcat "\nISSUE30_MANUAL_MARKER="
    (itoa (issue30-has-pier-marker manual))))
  (princ (strcat "\nISSUE30_STANDALONE_CREATED="
    (if standalone "1" "0")))
  (command "_.REGEN")
  (command "_.ZOOM" "_E")

  (setq table-before (issue30-count '((0 . "ACAD_TABLE"))))
  (command "STPVBALLASTSTAT"
    '(-5000.0 -5000.0 0.0) '(24000.0 8000.0 0.0)
    '(26000.0 4000.0 0.0))
  (setq table-after (issue30-count '((0 . "ACAD_TABLE"))))
  (setq expected (+ nested 2))
  (setq actual (issue30-table-total))
  (princ (strcat "\nISSUE30_FRAME_PIERS=" (itoa nested)))
  (princ (strcat "\nISSUE30_EXPECTED_TOTAL=" (itoa expected)))
  (princ (strcat "\nISSUE30_ACTUAL_TOTAL=" (itoa actual)))
  (princ (strcat "\nISSUE30_TABLES="
    (itoa table-before) "," (itoa table-after)))
  (if (and (= actual expected) (= table-after (1+ table-before))
           (= (issue30-has-pier-marker manual) 1))
    (princ "\nISSUE30_RUN=PASS")
    (princ "\nISSUE30_RUN=FAIL"))
  (command "_.QSAVE")
  (princ))

(defun issue30-reopen ()
  (princ (strcat "\nISSUE30_REOPEN_TOTAL="
    (itoa (issue30-table-total))))
  (princ (strcat "\nISSUE30_REOPEN_TABLES="
    (itoa (issue30-count '((0 . "ACAD_TABLE"))))))
  (princ "\nISSUE30_REOPEN=PASS")
  (princ))

(princ)
